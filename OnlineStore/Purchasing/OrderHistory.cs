using Microsoft.EntityFrameworkCore;
using OnlineStore.Data;
using OnlineStore.Models;

namespace OnlineStore.Purchasing;

// One row of the customer's order history (SCRUM-76). Status is Processing, InTransit, Delivered or Cancelled.
public sealed record OrderSummary(long OrderId, string Status, string PaymentStatus, DateTime? PurchasedAt,
    DateTime? DeliveredAt, int ItemCount, decimal Total, string InvoiceStatus, string? InvoiceUrl);

// NextBeforeId is the id to send as ?beforeId= for the next (older) page, or null on the last page.
public sealed record OrderHistoryPage(IReadOnlyList<OrderSummary> Orders, long? NextBeforeId);

public static class OrderHistory
{
    public const int PageSize = 20;

    // The customer's paid orders, newest first. Unpaid attempts are not orders the customer has placed yet.
    // Only the rows of this customer are read, so one customer can never see another one's orders.
    public static async Task<OrderHistoryPage> ListAsync(StoreDbContext db, long userId, long? beforeId,
        CancellationToken cancellationToken, int pageSize = PageSize)
    {
        var query = db.Orders.AsNoTracking()
            .Where(order => order.UserId == userId && order.PaymentStatus == PaymentStatus.Confirmed);
        if (beforeId is > 0) query = query.Where(order => order.Id < beforeId);

        // One extra row tells whether an older page exists.
        var rows = await query.OrderByDescending(order => order.Id).Take(pageSize + 1)
            .Select(order => new
            {
                order.Id, order.Status, order.PaymentStatus, order.PurchasedAt, order.DeliveredAt,
                ItemCount = order.OrderItems.Sum(line => line.Quantity),
                Total = order.OrderItems.Sum(line => line.UnitPrice * line.Quantity),
                InvoiceReady = order.Invoice != null && order.Invoice.ProtectedDocumentKey != null
            })
            .ToListAsync(cancellationToken);

        var page = rows.Take(pageSize).Select(row => new OrderSummary(row.Id, row.Status.ToString(),
            row.PaymentStatus.ToString(), row.PurchasedAt, row.DeliveredAt, row.ItemCount, row.Total,
            row.InvoiceReady ? "Ready" : "Pending",
            row.InvoiceReady ? $"/api/customer/orders/{row.Id}/invoice" : null)).ToList();
        return new OrderHistoryPage(page, rows.Count > pageSize ? page[^1].OrderId : null);
    }
}
