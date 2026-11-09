namespace OnlineStore.Models;

// A 1-5 star rating. It is visible immediately, no approval is needed.
public partial class Rating
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public long ProductId { get; set; }

    // The delivered order line that proves the user bought the product.
    public long OrderItemId { get; set; }
    public byte Stars { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Product Product { get; set; } = null!;
    public OrderItem OrderItem { get; set; } = null!;
}
