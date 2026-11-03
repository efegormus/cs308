namespace OnlineStore.Models;

public class Discount
{
    public int Id { get; set; }

    // 0.15 means 15% off.
    public decimal Rate { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }

    public ICollection<DiscountProduct> Products { get; set; } = new List<DiscountProduct>();
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}

// Which products a discount applies to (many-to-many).
public class DiscountProduct
{
    public int DiscountId { get; set; }
    public int ProductId { get; set; }

    public Discount Discount { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
