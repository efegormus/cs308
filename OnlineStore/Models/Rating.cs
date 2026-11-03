namespace OnlineStore.Models;

// A 1-5 star rating. It is visible immediately, no approval is needed.
public class Rating
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int ProductId { get; set; }

    // The delivered order line that proves the user bought the product.
    public int OrderItemId { get; set; }
    public byte Stars { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Product Product { get; set; } = null!;
    public OrderItem OrderItem { get; set; } = null!;
}
