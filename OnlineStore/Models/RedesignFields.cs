// SQL contract: docs/db-redesign/schema.sql. Generated scalar additions; behavior stays in the domain files.
namespace OnlineStore.Models;

public partial class Address
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string Label { get; set; } = "";
    public string Text { get; set; } = "";
    public int DataKeyVersion { get; set; } = 1;
    public bool IsDefault { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public partial class AttributeDefinition
{
    public Category Category { get; set; } = null!;
    public long Id { get; set; }
    public long CategoryId { get; set; }
    public string Code { get; set; } = "";
    public string Label { get; set; } = "";
    public string DataType { get; set; } = "";
    public string? Unit { get; set; }
    public bool IsFilterable { get; set; } = false;
    public int SortOrder { get; set; }
}

public partial class Brand
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public partial class CartItem
{
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public partial class Cart
{
    public DateTime RowVersion { get; set; } = DateTime.UtcNow;
}

public partial class Category
{
    public Category? Parent { get; set; }
    public long? ParentId { get; set; }
    public string Slug { get; set; } = "";
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public partial class ChatAttachment
{
    public byte[] Sha256 { get; set; } = Array.Empty<byte>();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public partial class ChatMessage
{
    public string SenderKind { get; set; } = "";
    public DateTime? ReadAt { get; set; }
}

public partial class Comment
{
    public string? ModerationNote { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public partial class Conversation
{
    public Guid PublicId { get; set; } = Guid.NewGuid();
    public DateTime? ClaimedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
}

public partial class Delivery
{
    public long UserId { get; set; }
    public int DataKeyVersion { get; set; } = 1;
    public DateTime RowVersion { get; set; } = DateTime.UtcNow;
}

public partial class DeliveryItem
{
    public long OrderId { get; set; }
}

public partial class DiscountProduct
{
}

public partial class Discount
{
    public string Name { get; set; } = "";
    public long CreatedByUserId { get; set; }
    public DateTime? CancelledAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public partial class Distributor
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string ContactInfo { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public partial class InvoiceEmailDelivery
{
    public long Id { get; set; }
    public string DedupKey { get; set; } = "";
    public string Kind { get; set; } = "";
    public long RecipientUserId { get; set; }
    public int DataKeyVersion { get; set; } = 1;
    public long? InvoiceId { get; set; }
    public long? RefundId { get; set; }
    public string Status { get; set; } = "Pending";
}

public partial class Invoice
{
    public int DataKeyVersion { get; set; } = 1;
    public string CurrencyCode { get; set; } = "TRY";
    public decimal Subtotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal ShippingTotal { get; set; }
    public decimal GrandTotal { get; set; }
    public byte[] PdfSha256 { get; set; } = Array.Empty<byte>();
}

public partial class Notification
{
    public long? DiscountId { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTime? ReadAt { get; set; }
}

public partial class OrderItem
{
    public long UserId { get; set; }
    public string Sku { get; set; } = "";
    public decimal ListUnitPrice { get; set; }
    public decimal? DiscountRate { get; set; }
    public decimal LineTotal { get; set; }
    public int DeliveryAllocatedQuantity { get; set; }
    public int DeliveredQuantity { get; set; }
    public int ReturnRequestedQuantity { get; set; }
    public int RefundedQuantity { get; set; }
    public DateTime RowVersion { get; set; } = DateTime.UtcNow;
}

public partial class Order
{
    public string OrderNumber { get; set; } = Guid.NewGuid().ToString("N")[..24];
    public string CurrencyCode { get; set; } = "TRY";
    public int DataKeyVersion { get; set; } = 1;
    public decimal Subtotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal ShippingTotal { get; set; }
    public decimal GrandTotal { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelReason { get; set; }
}

public partial class Payment
{
    public long UserId { get; set; }
    public long CartId { get; set; }
    public string CurrencyCode { get; set; } = "TRY";
    public string CardBrand { get; set; } = "";
    public string CardLast4 { get; set; } = "";
    public string? FailureCode { get; set; }
}

public partial class ProductAttributeValue
{
    public AttributeDefinition Definition { get; set; } = null!;
    public long ProductId { get; set; }
    public long AttributeDefinitionId { get; set; }
    public string DataType { get; set; } = "";
    public string? ValueText { get; set; }
    public decimal? ValueNumber { get; set; }
    public bool? ValueBool { get; set; }
}

public partial class ProductImage
{
    public long Id { get; set; }
    public long ProductId { get; set; }
    public string StorageKey { get; set; } = "";
    public string? AltText { get; set; }
    public int SortOrder { get; set; }
    public bool IsPrimary { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public partial class Product
{
    public long? BrandId { get; set; }
    public long DistributorId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime RowVersion { get; set; } = DateTime.UtcNow;
}

public partial class Rating
{
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public partial class Refund
{
    public long OrderId { get; set; }
    public string Reason { get; set; } = "";
    public string CurrencyCode { get; set; } = "TRY";
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public partial class ReturnRequest
{
    public long ProductId { get; set; }
    public string Reason { get; set; } = "";
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }
}

public partial class User
{
    public Guid PublicId { get; set; } = Guid.NewGuid();
    public byte[] EmailBlindIndex { get; set; } = Array.Empty<byte>();
    public int DataKeyVersion { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public int FailedLoginCount { get; set; }
    public DateTime? LockoutUntil { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime RowVersion { get; set; } = DateTime.UtcNow;
}

public partial class WishlistItem
{
}
