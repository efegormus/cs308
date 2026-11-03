namespace OnlineStore.Models;

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }

    // Purchase-time snapshots; do not read historical values from Product.
    public required string ProductName { get; set; }
    public decimal UnitPrice { get; set; }
    // Null only for older orders whose actual purchase-time cost is unknown.
    public decimal? UnitCost { get; set; }
    public int Quantity { get; set; }

    public Order Order { get; set; } = null!;
    public Product Product { get; set; } = null!;

    public static OrderItem Snapshot(Product product, int quantity)
    {
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        return new OrderItem
        {
            Product = product, ProductId = product.Id, ProductName = product.Name,
            UnitPrice = product.Price, UnitCost = product.EffectiveCost, Quantity = quantity
        };
    }
}
