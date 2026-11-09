namespace OnlineStore.Models;

public partial class Product
{
    public long Id { get; set; }
    public long CategoryId { get; set; }
    public required string Sku { get; set; }
    public required string Name { get; set; }
    public string Model { get; set; } = "";
    public string SerialNumber { get; set; } = "";
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public string WarrantyStatus { get; set; } = "";
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string DistributorInformation
    {
        get => Distributor?.ContactInfo ?? "";
        set { Distributor ??= new Distributor { Name = "Distributor " + Guid.NewGuid().ToString("N") }; Distributor.ContactInfo = value; }
    }
    public Distributor Distributor { get; set; } = null!;
    public Brand? Brand { get; set; }
    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
    public ICollection<ProductAttributeValue> Attributes { get; set; } = new List<ProductAttributeValue>();
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? ImageUrl
    {
        get => Images.FirstOrDefault(i => i.IsPrimary)?.StorageKey;
        set
        {
            var image = Images.FirstOrDefault(i => i.IsPrimary);
            if (value is null) { if (image is not null) Images.Remove(image); }
            else if (image is null) Images.Add(new ProductImage { StorageKey = value, IsPrimary = true });
            else image.StorageKey = value;
        }
    }
    public bool IsActive { get; set; } = true;

    // Set by the product manager; null means "use the default".
    public decimal? Cost { get; set; }

    // Cost used for profit/loss: the given cost, or 50% of the price by default.
    public decimal EffectiveCost => Cost ?? Math.Round(Price * 0.5m, 2);

    public Category Category { get; set; } = null!;
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
