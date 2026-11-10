namespace OnlineStore.Dashboards;

/// <summary>Basic profile information for the signed-in customer.</summary>
public sealed record CustomerDashboardProfile(long Id, string Name, string Email);

/// <summary>A confirmed customer order shown on the dashboard.</summary>
public sealed record CustomerDashboardOrder(long OrderId, string Status, string PaymentStatus,
    DateTimeOffset? PurchasedAt, DateTimeOffset? DeliveredAt, int ItemCount, decimal Total,
    string InvoiceStatus, string? InvoiceUrl);

/// <summary>A product in the customer's limited wishlist preview.</summary>
public sealed record WishlistProductSummary(long Id, string Name, decimal Price, int StockQuantity, string? ImageUrl);

/// <summary>The customer dashboard's recent orders, wishlist preview, and basic profile.</summary>
public sealed record CustomerDashboardResponse(CustomerDashboardProfile Profile,
    IReadOnlyList<CustomerDashboardOrder> RecentConfirmedOrders, int WishlistCount,
    IReadOnlyList<WishlistProductSummary> WishlistItems);

/// <summary>A recent invoice summary without customer or tax snapshots.</summary>
public sealed record RecentInvoiceSummary(long OrderId, string InvoiceNumber, DateTimeOffset IssuedAt,
    decimal Total, string CurrencyCode);

/// <summary>An active discount and the number of products to which it applies.</summary>
public sealed record ActiveDiscountSummary(long Id, string Name, decimal Rate, DateTimeOffset StartsAt,
    DateTimeOffset EndsAt, int ProductCount);

/// <summary>Revenue and recent commercial activity for a requested half-open UTC period.</summary>
public sealed record SalesDashboardResponse(DateTimeOffset From, DateTimeOffset To, string CurrencyCode,
    decimal GrossRevenue, decimal Refunds, decimal NetRevenue,
    IReadOnlyList<RecentInvoiceSummary> RecentInvoices, IReadOnlyList<ActiveDiscountSummary> ActiveDiscounts);

/// <summary>A product at or below the dashboard's low-stock threshold.</summary>
public sealed record LowStockProductSummary(long Id, string Sku, string Name, int StockQuantity);

/// <summary>A delivery that has not reached a terminal fulfillment status.</summary>
public sealed record PendingDeliverySummary(long Id, long OrderId, string Status, DateTimeOffset CreatedAt);

/// <summary>A customer comment awaiting product-manager approval.</summary>
public sealed record PendingCommentSummary(long Id, long ProductId, string ProductName, string Body,
    DateTimeOffset CreatedAt);

/// <summary>Low-stock products, pending deliveries, and comments awaiting approval.</summary>
public sealed record ProductManagementDashboardResponse(IReadOnlyList<LowStockProductSummary> LowStockProducts,
    IReadOnlyList<PendingDeliverySummary> PendingDeliveries,
    IReadOnlyList<PendingCommentSummary> CommentsWaitingForApproval);

/// <summary>A safe user-list summary that excludes email, tax, address, and credential data.</summary>
public sealed record ManagementUserSummary(long Id, string Name, string Role, bool IsActive,
    DateTimeOffset CreatedAt);

/// <summary>A bounded page of manager-visible user summaries.</summary>
public sealed record ManagementUsersResponse(int Page, int PageSize, int TotalCount,
    IReadOnlyList<ManagementUserSummary> Users);
