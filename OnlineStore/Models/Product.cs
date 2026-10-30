namespace OnlineStore.Models;

public class Product
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public required string Sku { get; set; }
    public required string Name { get; set; }
    public string Model { get; set; } = "";
    public string SerialNumber { get; set; } = "";
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public string WarrantyStatus { get; set; } = "";
    public string DistributorInformation { get; set; } = "";
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;

    // Set by the product manager; null means "use the default".
    public decimal? Cost { get; set; }

    // Cost used for profit/loss: the given cost, or 50% of the price by default.
    public decimal EffectiveCost => Cost ?? Math.Round(Price * 0.5m, 2);

    public Category Category { get; set; } = null!;
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
