namespace OnlineStore.Models;

public partial class WishlistItem
{
    public long UserId { get; set; }
    public long ProductId { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    public Product Product { get; set; } = null!;
}
