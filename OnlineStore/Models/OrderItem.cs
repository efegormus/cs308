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

    public static OrderItem Snapshot(Product product, int quantity, decimal? paidUnitPrice = null)
    {
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        var price = paidUnitPrice ?? product.Price;
        if (price < 0) throw new ArgumentOutOfRangeException(nameof(paidUnitPrice));
        return new OrderItem
        {
            Product = product, ProductId = product.Id, ProductName = product.Name,
            UnitPrice = price, UnitCost = product.Cost ?? Math.Round(price * 0.5m, 2), Quantity = quantity
        };
    }
}
