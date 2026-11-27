using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using OnlineStore.Data;
using OnlineStore.Models;

namespace OnlineStore.Purchasing;

// SCRUM-157. AlreadyCancelled is true when the order was cancelled by an earlier request; nothing changes then.
public sealed record CancelledOrder(long OrderId, string Status, DateTime CancelledAt, bool AlreadyCancelled, int ItemsRestored, decimal Refunded);

public sealed class CancellationService(StoreDbContext db, TimeProvider clock)
{
    // A customer may cancel only their own order, and only while it is Processing. The order, its shipment and the stock
    // are changed in one transaction, so stock is never restored twice and never restored for an order that stays open.
    public async Task<CancelledOrder> CancelAsync(long userId, long orderId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        // Same lock order as DeliveryService: the order first, then its shipments. Another customer's id finds nothing (404).
        var order = (await db.Orders.FromSqlInterpolated(
            $"SELECT * FROM Orders WHERE Id = {orderId} AND UserId = {userId} FOR UPDATE").ToListAsync(ct)).SingleOrDefault()
            ?? throw new PurchaseException(404, "Order not found.");
        if (order.Status == OrderStatus.Cancelled && order.CancelledAt is { } earlier)
            return new CancelledOrder(order.Id, nameof(OrderStatus.Cancelled), earlier, true, 0, 0m);
        if (order.PaymentStatus != PaymentStatus.Confirmed)
            throw new PurchaseException(409, "Only a paid order can be cancelled.");

        var deliveries = await db.Deliveries.FromSqlInterpolated(
            $"SELECT * FROM Deliveries WHERE OrderId = {orderId} ORDER BY Id FOR UPDATE").ToListAsync(ct);
        await db.Entry(order).Collection(value => value.OrderItems).LoadAsync(ct);

        var now = clock.GetUtcNow().UtcDateTime;
        now = now.AddTicks(-(now.Ticks % 10)); // Match MySQL datetime(6) before persisting and returning it.
        if (!order.TransitionTo(OrderStatus.Cancelled, now))
            throw new PurchaseException(409, "Only an order that is still being processed can be cancelled.");
        order.CancelReason = "Cancelled by the customer";
        foreach (var delivery in deliveries.Where(value => value.Status == OrderStatus.Processing))
            delivery.Status = OrderStatus.Cancelled;

        // The stock taken at checkout goes back, product by product in id order (the same order checkout locks them in).
        foreach (var line in order.OrderItems.OrderBy(value => value.ProductId))
        {
            var quantity = line.Quantity;
            var changed = await db.Products.Where(product => product.Id == line.ProductId)
                .ExecuteUpdateAsync(update => update.SetProperty(product => product.StockQuantity,
                    product => product.StockQuantity + quantity), ct);
            if (changed != 1) throw new PurchaseException(409, "A product of this order no longer exists. Contact support.");
        }

        // The mock gateway has no refund call, so the refund is recorded as Completed right away. The payment itself stays
        // Confirmed: reports (SCRUM-152) show revenue as payments minus refunds. One cancellation refund per order is
        // enforced by the database (UX_Refunds_OneCancellation) and the key below.
        var payment = await db.Payments.SingleAsync(value => value.OrderId == orderId && value.Status == PaymentStatus.Confirmed, ct);
        db.Refunds.Add(new Refund
        {
            OrderId = orderId, PaymentId = payment.Id, Reason = "Cancellation", Amount = payment.Amount,
            IdempotencyKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"cancel:{orderId}"))),
            Status = "Completed", CreatedAt = now, RefundedAt = now
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new CancelledOrder(order.Id, nameof(OrderStatus.Cancelled), now, false,
            order.OrderItems.Sum(value => value.Quantity), payment.Amount);
    }
}
