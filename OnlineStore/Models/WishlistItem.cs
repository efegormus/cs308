namespace OnlineStore.Models;

public class WishlistItem
{
    public int UserId { get; set; }
    public int ProductId { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    public Product Product { get; set; } = null!;
}
