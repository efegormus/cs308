namespace OnlineStore.Models;

// A message for a user (for example "a product on your wishlist is discounted").
public class Notification
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int? ProductId { get; set; }
    public NotificationKind Kind { get; set; }

    // Same event never creates two notifications, even if the job runs twice.
    public required string DeduplicationKey { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }

    public Product? Product { get; set; }
}
