using System.Data;
using System.Globalization;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using OnlineStore.Data;
using OnlineStore.Models;

namespace OnlineStore.Purchasing;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DeliveryTransition(string? Status, string? Version);
public sealed record DeliveryLine(int ProductId, string Name, int Quantity, decimal Total);
public sealed record DeliveryView(int Id, int OrderId, int CustomerId, string Address, string Status,
    string PaymentStatus, DateTime CreatedAt, DateTime? DeliveredAt, string Version,
    IReadOnlyList<DeliveryLine> Items, decimal Total, bool CanShip, bool CanDeliver);
public sealed record DeliveryPage(IReadOnlyList<DeliveryView> Items, int? NextAfterId);

public sealed class DeliveryService(StoreDbContext db, TimeProvider clock)
{
    public async Task<DeliveryPage> ListAsync(int afterId, CancellationToken ct)
    {
        if (afterId < 0) throw new PurchaseException(400, "Use a non-negative delivery cursor.");
        var deliveries = await db.Deliveries.AsNoTrackingWithIdentityResolution().Where(delivery => delivery.Id > afterId)
            .OrderBy(delivery => delivery.Id).Take(51)
            .Include(delivery => delivery.Order).ThenInclude(order => order.OrderItems)
            .Include(delivery => delivery.Order).ThenInclude(order => order.Deliveries)
            .Include(delivery => delivery.Items).ThenInclude(line => line.OrderItem).ToListAsync(ct);
        return new DeliveryPage(deliveries.Take(50).Select(View).ToList(),
            deliveries.Count > 50 ? deliveries[49].Id : null);
    }

    public async Task<DeliveryView> TransitionAsync(int id, DeliveryTransition input, CancellationToken ct)
    {
        if (!Enum.TryParse<OrderStatus>(input.Status, false, out var target)
            || target is not (OrderStatus.InTransit or OrderStatus.Delivered) || string.IsNullOrEmpty(input.Version))
            throw new PurchaseException(400, "Use InTransit or Delivered and the current delivery version.");
        var orderId = await db.Deliveries.AsNoTracking().Where(delivery => delivery.Id == id)
            .Select(delivery => (int?)delivery.OrderId).SingleOrDefaultAsync(ct)
            ?? throw new PurchaseException(404, "Delivery not found.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        // All fulfillment writers lock the order first, then its shipment, and use the order guard.
        var order = (await db.Orders.FromSqlInterpolated($"SELECT * FROM Orders WHERE Id = {orderId} FOR UPDATE")
            .ToListAsync(ct)).Single();
        var deliveries = await db.Deliveries.FromSqlInterpolated(
            $"SELECT * FROM Deliveries WHERE OrderId = {orderId} ORDER BY Id FOR UPDATE").ToListAsync(ct);
        var delivery = deliveries.Single(value => value.Id == id);
        await db.Entry(order).Collection(value => value.OrderItems).LoadAsync(ct);
        await db.Entry(delivery).Collection(value => value.Items).LoadAsync(ct);
        if (!string.Equals(Version(order), input.Version, StringComparison.Ordinal))
            throw new PurchaseException(409, "The order changed. Reload before trying again.");
        if (!Consistent(delivery) || order.PaymentStatus != PaymentStatus.Confirmed)
            throw new PurchaseException(409, "This delivery needs reconciliation before fulfillment.");
        var now = clock.GetUtcNow().UtcDateTime;
        if (!order.TransitionTo(target, now))
            throw new PurchaseException(409, "That fulfillment transition is not allowed.");
        delivery.Status = target;
        delivery.DeliveredAt = order.DeliveredAt;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return View(delivery);
    }

    private static bool Consistent(Delivery delivery)
    {
        var order = delivery.Order;
        // This MVP ships a whole order once. Do not silently complete legacy split/incomplete shipments.
        return order.Deliveries.Count == 1 && order.Status == delivery.Status
            && !string.IsNullOrWhiteSpace(order.AddressSnapshot) && order.AddressSnapshot == delivery.AddressSnapshot
            && order.OrderItems.Count > 0 && order.OrderItems.Count == delivery.Items.Count
            && delivery.Items.All(line => line.OrderItem.OrderId == order.Id
                && line.Quantity == line.OrderItem.Quantity
                && line.TotalPriceSnapshot == line.OrderItem.UnitPrice * line.Quantity);
    }

    private static string Version(Order order) => order.RowVersion.Ticks.ToString(CultureInfo.InvariantCulture);
    private static DeliveryView View(Delivery delivery)
    {
        var paid = delivery.Order.PaymentStatus == PaymentStatus.Confirmed;
        var valid = Consistent(delivery);
        return new DeliveryView(delivery.Id, delivery.OrderId, delivery.Order.UserId, delivery.AddressSnapshot,
            delivery.Status.ToString(), delivery.Order.PaymentStatus.ToString(), delivery.CreatedAt, delivery.DeliveredAt,
            Version(delivery.Order), delivery.Items.OrderBy(line => line.OrderItem.ProductId)
                .Select(line => new DeliveryLine(line.OrderItem.ProductId, line.OrderItem.ProductName,
                    line.Quantity, line.TotalPriceSnapshot)).ToList(),
            delivery.Items.Sum(line => line.TotalPriceSnapshot),
            paid && valid && delivery.Status == OrderStatus.Processing,
            paid && valid && delivery.Status == OrderStatus.InTransit);
    }
}
