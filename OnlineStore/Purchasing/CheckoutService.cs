using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using OnlineStore.Accounts;
using OnlineStore.Data;
using OnlineStore.Models;

namespace OnlineStore.Purchasing;

public sealed record CheckoutResult(PurchaseReceipt Receipt, bool Replayed);

public sealed class CheckoutService(StoreDbContext db, ICheckoutPaymentGateway payments, TimeProvider clock)
{
    private const decimal MaxMoney = 9999999999999999.99m;

    public async Task<CheckoutQuote> QuoteAsync(int userId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleAsync(user => user.Id == userId, ct);
        ValidateCustomer(user);
        var carts = await db.Carts.AsNoTracking().Where(cart => cart.UserId == userId && cart.State == CartState.Open)
            .Include(cart => cart.Items).ThenInclude(line => line.Product).Take(2).ToListAsync(ct);
        if (carts.Count == 0) throw new PurchaseException(409, "Your cart is empty. Add products before checkout.");
        if (carts.Count != 1) throw new PurchaseException(409, "Your carts need to be merged before checkout.");
        return (await BuildQuoteAsync(user, carts[0], clock.GetUtcNow().UtcDateTime, ct)).Quote;
    }

    public async Task<CheckoutResult> CompleteAsync(int userId, CheckoutRequest input, CancellationToken ct)
    {
        if (input.CartId <= 0 || !Guid.TryParseExact(input.IdempotencyKey, "D", out var key)
            || input.QuoteToken is not { Length: 64 })
            throw new PurchaseException(400, "A cart, current quote and UUID idempotency key are required.");
        var attemptKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{userId}:{key:D}")));

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        // Serialize purchases by an identity, including retries before the first attempt commits.
        // Cart mutations/merge must lock the same cart row before changing its lines (see the API contract).
        var user = (await db.Users.FromSqlInterpolated($"SELECT * FROM Users WHERE Id = {userId} FOR UPDATE")
            .ToListAsync(ct)).SingleOrDefault() ?? throw new PurchaseException(401, "Please log in again.");
        if (user.Role != UserRole.Customer) throw new PurchaseException(403, "A customer account is required.");
        var previous = await db.Payments.AsNoTracking().Where(payment => payment.IdempotencyKey == attemptKey)
            .Include(payment => payment.Order).ThenInclude(order => order.OrderItems)
            .Include(payment => payment.Order).ThenInclude(order => order.Invoice).SingleOrDefaultAsync(ct);
        if (previous is not null)
        {
            if (previous.Order.UserId != userId || previous.Order.SourceCartId != input.CartId)
                throw new PurchaseException(409, "This retry key belongs to a different checkout.");
            await transaction.CommitAsync(ct);
            return new CheckoutResult(PurchaseViews.Receipt(previous.Order), true);
        }

        ValidateCustomer(user);
        var cart = (await db.Carts.FromSqlInterpolated($"SELECT * FROM Carts WHERE Id = {input.CartId} FOR UPDATE")
            .ToListAsync(ct)).SingleOrDefault();
        if (cart is null || cart.UserId != userId) throw new PurchaseException(404, "Cart not found.");
        if (cart.State != CartState.Open) throw new PurchaseException(409, "This cart has already been checked out.");
        cart.Items = await db.CartItems.FromSqlInterpolated(
            $"SELECT * FROM CartItems WHERE CartId = {cart.Id} ORDER BY ProductId FOR UPDATE").ToListAsync(ct);
        if (cart.Items.Count is 0 or > 100) throw new PurchaseException(409, "Use a cart with 1 to 100 product lines.");
        // Every checkout locks products in the same order to avoid competing stock updates/deadlocks.
        foreach (var line in cart.Items.OrderBy(line => line.ProductId))
        {
            line.Product = (await db.Products.FromSqlInterpolated(
                $"SELECT * FROM Products WHERE Id = {line.ProductId} FOR UPDATE").ToListAsync(ct)).Single();
        }
        var instant = clock.GetUtcNow().UtcDateTime;
        var now = instant.AddTicks(-(instant.Ticks % 10)); // MySQL datetime(6) precision, including the first response.
        var (quote, discounts) = await BuildQuoteAsync(user, cart, now, ct);
        if (!string.Equals(quote.QuoteToken, input.QuoteToken, StringComparison.Ordinal))
            throw new PurchaseException(409, "Your cart, profile or prices changed. Review the updated checkout.");
        if (input.Payment is null) throw new PurchaseException(400, "Mock payment details are required.");
        var approval = await payments.AuthorizeAsync(input.Payment, quote.Total, attemptKey, ct);
        if (!approval.Approved) throw new PurchaseException(402, "The mock payment was declined. Your cart was kept.");
        if (string.IsNullOrWhiteSpace(approval.Reference) || approval.Reference.Length > 100)
            throw new PurchaseException(503, "The payment service is temporarily unavailable.");

        var order = new Order
        {
            UserId = userId, SourceCartId = cart.Id, CreatedAt = now,
            CustomerNameSnapshot = user.Name, TaxIdSnapshot = user.TaxId,
            EmailSnapshot = user.Email, AddressSnapshot = user.HomeAddress
        };
        foreach (var line in cart.Items.OrderBy(line => line.ProductId))
        {
            var price = quote.Items.Single(item => item.ProductId == line.ProductId).UnitPrice;
            var snapshot = OrderItem.Snapshot(line.Product, line.Quantity, price);
            snapshot.DiscountId = discounts.GetValueOrDefault(line.ProductId)?.Id;
            order.OrderItems.Add(snapshot);
            // Defense in depth: the row lock AND the affected-row guard prevent overselling.
            var changed = await db.Products.Where(product => product.Id == line.ProductId
                    && product.IsActive && product.StockQuantity >= line.Quantity)
                .ExecuteUpdateAsync(update => update.SetProperty(product => product.StockQuantity,
                    product => product.StockQuantity - line.Quantity), ct);
            if (changed != 1) throw new PurchaseException(409, "Stock changed. Review your cart before trying again.");
        }
        order.ConfirmPayment(now);
        order.Payments.Add(new Payment
        {
            IdempotencyKey = attemptKey, ProviderReference = approval.Reference,
            Amount = quote.Total, Status = PaymentStatus.Confirmed, CreatedAt = now, ConfirmedAt = now
        });
        order.Deliveries.Add(new Delivery
        {
            AddressSnapshot = user.HomeAddress, CreatedAt = now,
            Items = order.OrderItems.Select(line => new DeliveryItem
            {
                OrderItem = line, Quantity = line.Quantity, TotalPriceSnapshot = line.UnitPrice * line.Quantity
            }).ToList()
        });
        db.Orders.Add(order);
        cart.State = CartState.CheckedOut;
        cart.UpdatedAt = now;
        db.CartItems.RemoveRange(cart.Items);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new CheckoutResult(PurchaseViews.Receipt(order), false);
    }

    private async Task<(CheckoutQuote Quote, Dictionary<int, Discount> Discounts)> BuildQuoteAsync(
        User user, Cart cart, DateTime now, CancellationToken ct)
    {
        if (cart.Items.Count is 0 or > 100) throw new PurchaseException(409, "Use a cart with 1 to 100 product lines.");
        if (cart.Items.Any(line => line.Quantity <= 0 || !line.Product.IsActive || line.Quantity > line.Product.StockQuantity))
            throw new PurchaseException(409, "Some products are unavailable or exceed stock. Update your cart before checkout.");
        var productIds = cart.Items.Select(line => line.ProductId).ToArray();
        var active = await db.DiscountProducts.AsNoTracking().Where(link => productIds.Contains(link.ProductId)
                && link.Discount.StartsAt <= now && link.Discount.EndsAt > now)
            .Include(link => link.Discount).ToListAsync(ct);
        // Overlapping campaigns use the best single rate, never stack discounts.
        var discounts = active.GroupBy(link => link.ProductId).ToDictionary(group => group.Key,
            group => group.OrderByDescending(link => link.Discount.Rate).ThenBy(link => link.DiscountId).First().Discount);
        var lines = cart.Items.OrderBy(line => line.ProductId).Select(line =>
        {
            var rate = discounts.GetValueOrDefault(line.ProductId)?.Rate ?? 0;
            var price = Math.Round(line.Product.Price * (1 - rate), 2, MidpointRounding.AwayFromZero);
            return new CheckoutLine(line.ProductId, line.Product.Name, line.Quantity, price, price * line.Quantity);
        }).ToList();
        var total = lines.Sum(line => line.Total);
        if (total > MaxMoney) throw new PurchaseException(409, "The cart total exceeds the supported purchase limit.");
        // A stale quote is rejected. This is a change detector, not a client-supplied authoritative price.
        var contents = System.Text.Json.JsonSerializer.Serialize(new
        {
            cart.Id, user.Name, user.TaxId, user.Email, user.HomeAddress,
            Lines = lines.Select(line => new { line.ProductId, line.Name, line.Quantity,
                Price = line.UnitPrice.ToString("0.00", CultureInfo.InvariantCulture),
                DiscountId = discounts.GetValueOrDefault(line.ProductId)?.Id })
        });
        var token = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(contents)));
        return (new CheckoutQuote(cart.Id, token, "TRY", user.Name, user.TaxId, user.Email,
            user.HomeAddress, lines, total), discounts);
    }

    private static void ValidateCustomer(User user)
    {
        if (user.Role != UserRole.Customer) throw new PurchaseException(403, "A customer account is required.");
        if (CustomerProfileValidation.Validate(new(user.Name, user.Email, user.TaxId, user.HomeAddress)).Count != 0)
            throw new PurchaseException(409, "Complete your customer profile before checkout.");
    }
}
