using Microsoft.EntityFrameworkCore;
using OnlineStore.Models;
using OnlineStore.Purchasing;

namespace OnlineStore.Tests;

// SCRUM-157: a customer cancels an order only while it is Processing, and the stock goes back exactly once.
[Collection("MySql")]
public class CancellationMySqlTests(MySqlFixture fixture) : IClassFixture<MySqlFixture>
{
    private const int StartStock = 5;

    private static MockCard Card() => new()
        { Number = "4242424242424242", ExpiryMonth = 12, ExpiryYear = DateTime.UtcNow.Year + 1, SecurityCode = "123" };

    // One buyer, one other customer, a product with 5 in stock, and a paid order of 2 units (stock is now 3).
    private async Task<(User Buyer, User Other, Product Product, long OrderId)> PaidOrderAsync(int quantity = 2)
    {
        await fixture.ResetAsync();
        await using var db = fixture.Create();
        User NewUser(string email)
        {
            var user = new User { Name = "Buyer", Email = email, TaxId = "TAX-1", HomeAddress = "Test address", PasswordHash = "x", Role = UserRole.Customer };
            return user;
        }
        var buyer = NewUser("cancel-buyer@example.com");
        var other = NewUser("cancel-other@example.com");
        var product = new Product { Sku = "CANCEL-1", Name = "Cancel product", Price = 100, StockQuantity = StartStock,
            Category = new Category { Name = "Cancel category" } };
        CatalogFixture.CompleteDetails(product);
        db.Users.AddRange(buyer, other);
        db.Products.Add(product);
        await db.SaveChangesAsync();
        db.Carts.Add(new Cart { UserId = buyer.Id, Items = [new CartItem { ProductId = product.Id, Quantity = quantity }] });
        await db.SaveChangesAsync();
        var checkout = new CheckoutService(db, new MockPaymentGateway(TimeProvider.System), TimeProvider.System);
        var quote = await checkout.QuoteAsync(buyer.Id, default);
        var purchase = await checkout.CompleteAsync(buyer.Id,
            new CheckoutRequest(quote.CartId, Guid.NewGuid().ToString("D"), quote.QuoteToken, Card()), default);
        return (buyer, other, product, purchase.Receipt.OrderId);
    }

    private async Task<int> StockAsync(long productId)
    {
        await using var db = fixture.Create();
        return await db.Products.AsNoTracking().Where(product => product.Id == productId)
            .Select(product => product.StockQuantity).SingleAsync();
    }

    private async Task<CancelledOrder> CancelAsync(long userId, long orderId)
    {
        await using var db = fixture.Create();
        return await new CancellationService(db, TimeProvider.System).CancelAsync(userId, orderId, default);
    }

    private async Task<int> BlockedStatusAsync(long userId, long orderId) =>
        (await Assert.ThrowsAsync<PurchaseException>(() => CancelAsync(userId, orderId))).Status;

    private async Task ShipAsync(string target)
    {
        await using var db = fixture.Create();
        var service = new DeliveryService(db, TimeProvider.System);
        var delivery = (await service.ListAsync(0, default)).Items.Single();
        await service.TransitionAsync(delivery.Id, new(target, delivery.Version), default);
    }

    [MySqlFact]
    public async Task MySql_Cancel_ProcessingOrder_CancelsItsDeliveryAndRestoresStock()
    {
        var g = await PaidOrderAsync();
        Assert.Equal(StartStock - 2, await StockAsync(g.Product.Id));
        var result = await CancelAsync(g.Buyer.Id, g.OrderId);
        Assert.Equal("Cancelled", result.Status);
        Assert.False(result.AlreadyCancelled);
        Assert.Equal(2, result.ItemsRestored);
        Assert.Equal(200m, result.Refunded);
        Assert.Equal(StartStock, await StockAsync(g.Product.Id));
        await using var check = fixture.Create();
        var refund = await check.Refunds.AsNoTracking().SingleAsync();
        Assert.Equal("Cancellation", refund.Reason);
        Assert.Equal("Completed", refund.Status);
        Assert.Equal(200m, refund.Amount);
        Assert.Equal(g.OrderId, refund.OrderId);
        Assert.Null(refund.ReturnRequestId);
        var order = await check.Orders.SingleAsync(value => value.Id == g.OrderId);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.NotNull(order.CancelledAt);
        Assert.Equal(OrderStatus.Cancelled, (await check.Deliveries.SingleAsync()).Status);
    }

    [MySqlFact]
    public async Task MySql_Cancel_RepeatedRequest_IsSafeAndRestoresStockOnlyOnce()
    {
        var g = await PaidOrderAsync();
        var first = await CancelAsync(g.Buyer.Id, g.OrderId);
        var second = await CancelAsync(g.Buyer.Id, g.OrderId);
        Assert.False(first.AlreadyCancelled);
        Assert.True(second.AlreadyCancelled);
        Assert.Equal(0, second.ItemsRestored);
        Assert.Equal(first.CancelledAt, second.CancelledAt);
        Assert.Equal(StartStock, await StockAsync(g.Product.Id));
        await using var check = fixture.Create();
        Assert.Equal(1, await check.Refunds.CountAsync());
    }

    [MySqlFact]
    public async Task MySql_Cancel_TwoRequestsAtTheSameTime_RestoreStockOnce()
    {
        var g = await PaidOrderAsync();
        var results = await Task.WhenAll(CancelAsync(g.Buyer.Id, g.OrderId), CancelAsync(g.Buyer.Id, g.OrderId));
        Assert.Equal(1, results.Count(result => !result.AlreadyCancelled));
        Assert.Equal(StartStock, await StockAsync(g.Product.Id));
    }

    [MySqlFact]
    public async Task MySql_Cancel_AnotherCustomersOrder_Is404AndChangesNothing()
    {
        var g = await PaidOrderAsync();
        Assert.Equal(404, await BlockedStatusAsync(g.Other.Id, g.OrderId));
        Assert.Equal(StartStock - 2, await StockAsync(g.Product.Id));
        await using var check = fixture.Create();
        Assert.Equal(OrderStatus.Processing, (await check.Orders.SingleAsync(value => value.Id == g.OrderId)).Status);
    }

    [MySqlFact]
    public async Task MySql_Cancel_UnknownOrder_Is404()
    {
        var g = await PaidOrderAsync();
        Assert.Equal(404, await BlockedStatusAsync(g.Buyer.Id, g.OrderId + 1000));
    }

    [MySqlFact]
    public async Task MySql_Cancel_InTransitOrder_IsBlockedAndKeepsStock()
    {
        var g = await PaidOrderAsync();
        await ShipAsync("InTransit");
        Assert.Equal(409, await BlockedStatusAsync(g.Buyer.Id, g.OrderId));
        Assert.Equal(StartStock - 2, await StockAsync(g.Product.Id));
        await using var check = fixture.Create();
        Assert.Empty(await check.Refunds.ToListAsync());
        Assert.Equal(OrderStatus.InTransit, (await check.Orders.SingleAsync(value => value.Id == g.OrderId)).Status);
    }

    [MySqlFact]
    public async Task MySql_Cancel_DeliveredOrder_IsBlockedAndKeepsStock()
    {
        var g = await PaidOrderAsync();
        await ShipAsync("InTransit");
        await ShipAsync("Delivered");
        Assert.Equal(409, await BlockedStatusAsync(g.Buyer.Id, g.OrderId));
        Assert.Equal(StartStock - 2, await StockAsync(g.Product.Id));
    }

    [MySqlFact]
    public async Task MySql_Cancel_CancelledOrderStaysOutOfFulfillment()
    {
        var g = await PaidOrderAsync();
        await CancelAsync(g.Buyer.Id, g.OrderId);
        await using var db = fixture.Create();
        var service = new DeliveryService(db, TimeProvider.System);
        var delivery = (await service.ListAsync(0, default)).Items.Single();
        Assert.Equal("Cancelled", delivery.Status);
        Assert.False(delivery.CanShip);
        Assert.Equal(409, (await Assert.ThrowsAsync<PurchaseException>(
            () => service.TransitionAsync(delivery.Id, new("InTransit", delivery.Version), default))).Status);
    }
}
