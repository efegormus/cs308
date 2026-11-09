namespace OnlineStore.Models;

// A message for a user (for example "a product on your wishlist is discounted").
public partial class Notification
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public long? ProductId { get; set; }
    public NotificationKind Kind { get; set; }

    // Same event never creates two notifications, even if the job runs twice.
    public required string DeduplicationKey { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public DateTime? SentAt { get; set; }

    public Product? Product { get; set; }
}
