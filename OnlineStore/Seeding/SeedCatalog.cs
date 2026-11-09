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
    public string? ParentCode { get; init; }
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
}

public sealed record SeedProduct
{
    public string? Brand { get; init; }
    public string? ImageUrl { get; init; }
    public List<SeedSpecification> Specifications { get; init; } = new();
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

public sealed record SeedSpecification
{
    public string Code { get; init; } = "";
    public string Label { get; init; } = "";
    public string DataType { get; init; } = "Text";
    public string? Unit { get; init; }
    public string? Text { get; init; }
    public decimal? Number { get; init; }
    public bool? Bool { get; init; }
}
