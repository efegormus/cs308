namespace OnlineStore.Models;

// A written review. It stays hidden until the product manager approves it.
public partial class Comment
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public long ProductId { get; set; }
    public long OrderItemId { get; set; }
    public long? ModeratedByUserId { get; set; }
    public required string Body { get; set; }
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ModeratedAt { get; set; }

    public Product Product { get; set; } = null!;
    public OrderItem OrderItem { get; set; } = null!;
}
