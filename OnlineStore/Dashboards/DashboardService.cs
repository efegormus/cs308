using Microsoft.EntityFrameworkCore;
using OnlineStore.Data;
using OnlineStore.Models;
using OnlineStore.Purchasing;

namespace OnlineStore.Dashboards;

public interface IDashboardService
{
    Task<CustomerDashboardResponse?> GetCustomerAsync(long userId, CancellationToken cancellationToken);
    Task<SalesDashboardResponse> GetSalesAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);
    Task<ProductManagementDashboardResponse> GetProductManagementAsync(CancellationToken cancellationToken);
    Task<ManagementUsersResponse> ListUsersAsync(int page, int pageSize, string? search, CancellationToken cancellationToken);
}

public sealed class DashboardService(StoreDbContext db, TimeProvider clock) : IDashboardService
{
    public const int LowStockThreshold = 5;
    public const int LowStockLimit = 20;
    public const int WishlistPreviewLimit = 5;
    public const int RecentOrderLimit = 5;
    public const int RecentInvoiceLimit = 10;
    public const int ActiveDiscountLimit = 10;
    public const int PendingDeliveryLimit = 20;
    public const int PendingCommentLimit = 10;

    public async Task<CustomerDashboardResponse?> GetCustomerAsync(long userId, CancellationToken cancellationToken)
    {
        var profile = await db.Users.AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new CustomerDashboardProfile(user.Id, user.Name, user.Email))
            .SingleOrDefaultAsync(cancellationToken);
        if (profile is null) return null;

        var orders = await OrderHistory.ListAsync(db, userId, null, cancellationToken, RecentOrderLimit);
        var recentOrders = orders.Orders.Select(order => new CustomerDashboardOrder(
            order.OrderId, order.Status, order.PaymentStatus, ToUtc(order.PurchasedAt), ToUtc(order.DeliveredAt),
            order.ItemCount, order.Total, order.InvoiceStatus, order.InvoiceUrl)).ToList();

        var wishlistCount = await db.WishlistItems.AsNoTracking()
            .CountAsync(item => item.UserId == userId, cancellationToken);
        var wishlistItems = await db.WishlistItems.AsNoTracking()
            .Where(item => item.UserId == userId && item.Product.IsActive)
            .OrderByDescending(item => item.AddedAt).ThenBy(item => item.ProductId)
            .Take(WishlistPreviewLimit)
            .Select(item => new WishlistProductSummary(item.ProductId, item.Product.Name, item.Product.Price,
                item.Product.StockQuantity, item.Product.Images.OrderBy(image => image.SortOrder)
                    .ThenBy(image => image.Id).Select(image => image.StorageKey).FirstOrDefault()))
            .ToListAsync(cancellationToken);

        return new CustomerDashboardResponse(profile, recentOrders, wishlistCount, wishlistItems);
    }

    public async Task<SalesDashboardResponse> GetSalesAsync(DateTimeOffset from, DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        var start = from.UtcDateTime;
        var end = to.UtcDateTime;
        var grossRevenue = await db.Payments.AsNoTracking()
            .Where(payment => payment.Status == PaymentStatus.Confirmed && payment.OrderId != null
                && payment.ConfirmedAt >= start && payment.ConfirmedAt < end)
            .SumAsync(payment => payment.Amount, cancellationToken);
        var refunds = await db.Refunds.AsNoTracking()
            .Where(refund => refund.Status == "Completed" && refund.RefundedAt >= start && refund.RefundedAt < end)
            .SumAsync(refund => refund.Amount, cancellationToken);

        var invoices = await db.Invoices.AsNoTracking()
            .OrderByDescending(invoice => invoice.IssuedAt).ThenByDescending(invoice => invoice.Id)
            .Take(RecentInvoiceLimit)
            .Select(invoice => new
            {
                invoice.OrderId, invoice.InvoiceNumber, invoice.IssuedAt, invoice.GrandTotal, invoice.CurrencyCode
            })
            .ToListAsync(cancellationToken);

        var now = clock.GetUtcNow().UtcDateTime;
        var discounts = await db.Discounts.AsNoTracking()
            .Where(discount => discount.CancelledAt == null && discount.StartsAt <= now && discount.EndsAt > now)
            .OrderBy(discount => discount.EndsAt).ThenBy(discount => discount.Id)
            .Take(ActiveDiscountLimit)
            .Select(discount => new
            {
                discount.Id, discount.Name, discount.Rate, discount.StartsAt, discount.EndsAt,
                ProductCount = discount.Products.Count
            })
            .ToListAsync(cancellationToken);

        return new SalesDashboardResponse(from, to, "TRY", grossRevenue, refunds, grossRevenue - refunds,
            invoices.Select(invoice => new RecentInvoiceSummary(invoice.OrderId, invoice.InvoiceNumber,
                ToUtc(invoice.IssuedAt), invoice.GrandTotal, invoice.CurrencyCode)).ToList(),
            discounts.Select(discount => new ActiveDiscountSummary(discount.Id, discount.Name, discount.Rate,
                ToUtc(discount.StartsAt), ToUtc(discount.EndsAt), discount.ProductCount)).ToList());
    }

    public async Task<ProductManagementDashboardResponse> GetProductManagementAsync(CancellationToken cancellationToken)
    {
        var products = await db.Products.AsNoTracking()
            .Where(product => product.IsActive && product.StockQuantity <= LowStockThreshold)
            .OrderBy(product => product.StockQuantity).ThenBy(product => product.Name).ThenBy(product => product.Id)
            .Take(LowStockLimit)
            .Select(product => new LowStockProductSummary(product.Id, product.Sku, product.Name, product.StockQuantity))
            .ToListAsync(cancellationToken);

        var deliveryRows = await db.Deliveries.AsNoTracking()
            .Where(delivery => delivery.Status == OrderStatus.Processing || delivery.Status == OrderStatus.InTransit)
            .OrderByDescending(delivery => delivery.CreatedAt).ThenByDescending(delivery => delivery.Id)
            .Take(PendingDeliveryLimit)
            .Select(delivery => new { delivery.Id, delivery.OrderId, delivery.Status, delivery.CreatedAt })
            .ToListAsync(cancellationToken);
        var deliveries = deliveryRows.Select(delivery => new PendingDeliverySummary(delivery.Id,
            delivery.OrderId, delivery.Status.ToString(), ToUtc(delivery.CreatedAt))).ToList();

        var comments = await db.Comments.AsNoTracking()
            .Where(comment => comment.ApprovalStatus == ApprovalStatus.Pending)
            .OrderBy(comment => comment.CreatedAt).ThenBy(comment => comment.Id)
            .Take(PendingCommentLimit)
            .Select(comment => new
            {
                comment.Id, comment.ProductId, ProductName = comment.Product.Name, comment.Body, comment.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new ProductManagementDashboardResponse(products, deliveries,
            comments.Select(comment => new PendingCommentSummary(comment.Id, comment.ProductId, comment.ProductName,
                comment.Body.Length <= 1000 ? comment.Body : comment.Body[..1000], ToUtc(comment.CreatedAt))).ToList());
    }

    public async Task<ManagementUsersResponse> ListUsersAsync(int page, int pageSize, string? search,
        CancellationToken cancellationToken)
    {
        var users = db.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrEmpty(search))
            users = users.Where(user => user.Name.StartsWith(search));

        var totalCount = await users.CountAsync(cancellationToken);
        var offset = checked((page - 1) * pageSize);
        var rows = await users.OrderBy(user => user.Id).Skip(offset).Take(pageSize)
            .Select(user => new
            {
                user.Id, user.Name, user.Role, user.IsActive, user.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new ManagementUsersResponse(page, pageSize, totalCount,
            rows.Select(user => new ManagementUserSummary(user.Id, user.Name, user.Role.ToString(),
                user.IsActive, ToUtc(user.CreatedAt))).ToList());
    }

    private static DateTimeOffset? ToUtc(DateTime? value) => value is null ? null : ToUtc(value.Value);
    private static DateTimeOffset ToUtc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
