namespace OnlineStore.Models;

// A shopping cart. It belongs either to a logged-in user or to a guest.
// A guest is identified by a random token kept in a cookie; only its hash is stored here.
public class Cart
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public string? GuestTokenHash { get; set; }
    public CartState State { get; set; } = CartState.Open;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<CartItem> Items { get; set; } = new List<CartItem>();
}

public class CartItem
{
    public int Id { get; set; }
    public int CartId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }

    public Cart Cart { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
