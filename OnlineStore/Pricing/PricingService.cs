using Microsoft.EntityFrameworkCore;
using OnlineStore.Catalog;
using OnlineStore.Data;
using OnlineStore.Models;
using OnlineStore.Purchasing;

namespace OnlineStore.Pricing;

// SCRUM-155: the sales manager sets list prices and creates, lists and cancels discount campaigns.
// A campaign never edits Product.Price. Shoppers see the discounted price because every price read and
// checkout go through ProductPricing (docs/pricing.md), so a campaign takes effect and ends on its own.
public sealed class PricingService(StoreDbContext db, TimeProvider clock)
{
    public const int PageSize = 50;

    public async Task<SalesProductPage> ListProductsAsync(long managerId, long afterId, CancellationToken ct)
    {
        await RequireSalesManagerAsync(managerId, ct);
        if (afterId < 0) throw new PurchaseException(400, "Use a non-negative product cursor.");
        var rows = await ProductPricing.Query(db, Now()).Where(p => p.Product.Id > afterId)
            .OrderBy(p => p.Product.Id).Take(PageSize + 1)
            .Select(p => new SalesProduct(p.Product.Id, p.Product.Name, p.Product.Model, p.Product.StockQuantity,
                p.Product.IsActive, p.ListPrice, p.Price, p.DiscountRate))
            .ToListAsync(ct);
        return new(rows.Take(PageSize).ToList(), rows.Count > PageSize ? rows[PageSize - 1].Id : null);
    }

    public async Task<SalesProduct> SetPriceAsync(long managerId, long productId, PriceChange input, CancellationToken ct)
    {
        if (input.Validate().Count != 0) throw new PurchaseException(400, "Use a valid price.");
        await RequireSalesManagerAsync(managerId, ct);
        var product = productId <= 0 ? null : await db.Products.SingleOrDefaultAsync(p => p.Id == productId, ct);
        if (product is null) throw new PurchaseException(404, "Product not found.");
        // Only the list price changes. Paid prices and discount snapshots of past orders stay as they were.
        product.Price = input.Price!.Value;
        await db.SaveChangesAsync(ct);
        return await ProductPricing.Query(db, Now()).Where(p => p.Product.Id == productId)
            .Select(p => new SalesProduct(p.Product.Id, p.Product.Name, p.Product.Model, p.Product.StockQuantity,
                p.Product.IsActive, p.ListPrice, p.Price, p.DiscountRate))
            .SingleAsync(ct);
    }

    public async Task<DiscountPage> ListDiscountsAsync(long managerId, long beforeId, CancellationToken ct)
    {
        await RequireSalesManagerAsync(managerId, ct);
        if (beforeId < 0) throw new PurchaseException(400, "Use a non-negative discount cursor.");
        var now = Now();
        var rows = await db.Discounts.AsNoTracking().Where(d => beforeId == 0 || d.Id < beforeId)
            .OrderByDescending(d => d.Id).Take(PageSize + 1)
            .Select(d => new
            {
                d.Id, d.Name, d.Rate, d.StartsAt, d.EndsAt, d.CancelledAt,
                ProductIds = d.Products.Select(link => link.ProductId).OrderBy(id => id).ToList()
            }).ToListAsync(ct);
        var page = rows.Take(PageSize).Select(d => new DiscountView(d.Id, d.Name, d.Rate, d.StartsAt, d.EndsAt,
            StatusOf(d.StartsAt, d.EndsAt, d.CancelledAt, now), d.CancelledAt, d.ProductIds)).ToList();
        return new(page, rows.Count > PageSize ? rows[PageSize - 1].Id : null);
    }

    public async Task<DiscountView> CreateDiscountAsync(long managerId, DiscountRequest input, CancellationToken ct)
    {
        if (input.Validate().Count != 0) throw new PurchaseException(400, "Use a valid discount.");
        await RequireSalesManagerAsync(managerId, ct);
        var now = Now();
        var startsAt = input.StartsAt is null ? now : Micro(input.StartsAt.Value.UtcDateTime);
        var endsAt = Micro(input.EndsAt!.Value.UtcDateTime);
        if (endsAt <= startsAt) throw new PurchaseException(400, "The discount must end after it starts.");
        if (endsAt <= now) throw new PurchaseException(400, "The discount must end in the future.");

        var ids = input.ProductIds!;
        var found = await db.Products.AsNoTracking().Where(p => ids.Contains(p.Id) && p.IsActive)
            .Select(p => p.Id).ToListAsync(ct);
        if (found.Count != ids.Length) throw new PurchaseException(400, "Every discounted product must exist and be active.");

        var discount = new Discount
        {
            Name = input.Name!.Trim(), Rate = input.Rate!.Value, StartsAt = startsAt, EndsAt = endsAt,
            CreatedByUserId = managerId, CreatedAt = now,
            Products = ids.Select(id => new DiscountProduct { ProductId = id }).ToList()
        };
        db.Discounts.Add(discount);
        await db.SaveChangesAsync(ct);
        return View(discount, now);
    }

    // Cancelling is idempotent. A campaign that already ended keeps its Expired status and is left untouched.
    public async Task<DiscountView> CancelDiscountAsync(long managerId, long discountId, CancellationToken ct)
    {
        await RequireSalesManagerAsync(managerId, ct);
        var discount = discountId <= 0 ? null
            : await db.Discounts.Include(d => d.Products).SingleOrDefaultAsync(d => d.Id == discountId, ct);
        if (discount is null) throw new PurchaseException(404, "Discount not found.");
        var now = Now();
        if (discount.CancelledAt is null && discount.EndsAt > now)
        {
            discount.CancelledAt = now;
            await db.SaveChangesAsync(ct);
        }
        return View(discount, now);
    }

    public static string StatusOf(DateTime startsAt, DateTime endsAt, DateTime? cancelledAt, DateTime now) =>
        cancelledAt is not null ? "Cancelled" : now < startsAt ? "Scheduled" : now >= endsAt ? "Expired" : "Active";

    private static DiscountView View(Discount d, DateTime now) => new(d.Id, d.Name, d.Rate, d.StartsAt, d.EndsAt,
        StatusOf(d.StartsAt, d.EndsAt, d.CancelledAt, now), d.CancelledAt,
        d.Products.Select(link => link.ProductId).OrderBy(id => id).ToList());

    private DateTime Now() => Micro(clock.GetUtcNow().UtcDateTime);

    // datetime(6) keeps microseconds. Cutting the extra ticks makes the value read back exactly as written.
    private static DateTime Micro(DateTime value) => value.AddTicks(-(value.Ticks % 10));

    private async Task RequireSalesManagerAsync(long userId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null || !user.IsActive) throw new PurchaseException(401, "Please log in again.");
        if (user.Role != UserRole.SalesManager) throw new PurchaseException(403, "A sales manager account is required.");
    }
}
