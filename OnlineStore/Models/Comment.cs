namespace OnlineStore.Models;

// A written review. It stays hidden until the product manager approves it.
public class Comment
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int ProductId { get; set; }
    public int OrderItemId { get; set; }
    public int? ModeratedByUserId { get; set; }
    public required string Body { get; set; }
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ModeratedAt { get; set; }

    public Product Product { get; set; } = null!;
    public OrderItem OrderItem { get; set; } = null!;
}
