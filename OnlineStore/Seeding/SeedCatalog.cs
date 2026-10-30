namespace OnlineStore.Seeding;

// Temporary seed-data structures; these are NOT replacements for SCRUM-41's
// Product and Category database models.
public sealed record SeedCatalog
{
    public List<SeedCategory> Categories { get; init; } = new();
    public List<SeedProduct> Products { get; init; } = new();
}

public sealed record SeedCategory
{
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
}

public sealed record SeedProduct
{
    public string SeedKey { get; init; } = "";
    public string Name { get; init; } = "";
    public string Model { get; init; } = "";
    public string SerialNumber { get; init; } = "";
    public string Description { get; init; } = "";
    public int QuantityInStock { get; init; }
    public decimal Price { get; init; }
    public decimal Cost { get; init; }
    public string WarrantyStatus { get; init; } = "";
    public string DistributorInformation { get; init; } = "";
    public string CategoryCode { get; init; } = "";
}
