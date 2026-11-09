namespace OnlineStore.Models;

// A shopping cart. It belongs either to a logged-in user or to a guest.
// A guest is identified by a random token kept in a cookie; only its hash is stored here.
public partial class Cart
{
    public long Id { get; set; }
    public long? UserId { get; set; }
    public string? GuestTokenHash { get; set; }
    public CartState State { get; set; } = CartState.Open;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<CartItem> Items { get; set; } = new List<CartItem>();
}

public partial class CartItem
{
    public long Id { get; set; }
    public long CartId { get; set; }
    public long ProductId { get; set; }
    public int Quantity { get; set; }

    public Cart Cart { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
