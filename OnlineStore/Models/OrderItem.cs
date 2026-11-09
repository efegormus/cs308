namespace OnlineStore.Models;

public partial class OrderItem
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public long ProductId { get; set; }

    // Purchase-time snapshots; do not read historical values from Product.
    public required string ProductName { get; set; }
    public decimal UnitPrice { get; set; }
    // Null only for older orders whose actual purchase-time cost is unknown.
    public decimal? UnitCost { get; set; }
    public int Quantity { get; set; }

    // The discount applied when the product was bought, if any.
    public long? DiscountId { get; set; }

    public Order Order { get; set; } = null!;
    public Product Product { get; set; } = null!;
    public Discount? Discount { get; set; }

    public static OrderItem Snapshot(Product product, int quantity, decimal? paidUnitPrice = null)
    {
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        var price = paidUnitPrice ?? product.Price;
        if (price < 0) throw new ArgumentOutOfRangeException(nameof(paidUnitPrice));
        return new OrderItem
        {
            Product = product, ProductId = product.Id, ProductName = product.Name,
            Sku = product.Sku, ListUnitPrice = product.Price, LineTotal = price * quantity,
            UnitPrice = price, UnitCost = product.Cost ?? Math.Round(price * 0.5m, 2), Quantity = quantity
        };
    }
}
