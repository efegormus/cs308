using OnlineStore.Seeding;
using Xunit;

namespace OnlineStore.Tests;

public class SeedCatalogTests
{
    // From cs308/OnlineStore.Tests/bin/Debug/net8.0 back to cs308/.
    private static string SeedFilePath => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..",
        "SeedData", "seed-products.json"));

    private static SeedCatalog Load() => SeedCatalogReader.ReadFromFile(SeedFilePath);

    [Fact]
    public void SeedFile_CanBeRead() => Assert.NotNull(Load());

    [Fact]
    public void SeedFile_ContainsProducts() => Assert.NotEmpty(Load().Products);

    [Fact]
    public void SeedFile_ContainsCategories() => Assert.NotEmpty(Load().Categories);

    [Fact]
    public void SeedFile_ContainsPhoneAndTabletCategories()
    {
        var categories = Load().Categories;
        Assert.Contains(categories, c => c.Code == "PHONES");
        Assert.Contains(categories, c => c.Code == "TABLETS");
    }

    [Fact]
    public void SeedFile_ContainsProductsInBothCategories()
    {
        var products = Load().Products;
        Assert.Contains(products, p => p.CategoryCode == "PHONES");
        Assert.Contains(products, p => p.CategoryCode == "TABLETS");
    }

    [Fact]
    public void SeedFile_HasPositivePrices() =>
        Assert.All(Load().Products, p => Assert.True(p.Price > 0, p.SeedKey));

    [Fact]
    public void SeedFile_HasNonnegativeStock() =>
        Assert.All(Load().Products, p => Assert.True(p.QuantityInStock >= 0, p.SeedKey));

    [Fact]
    public void SeedFile_HasUniqueSeedKeys()
    {
        var products = Load().Products;
        Assert.Equal(products.Count, products.Select(p => p.SeedKey)
            .Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void SeedFile_HasUniqueSerialNumbers()
    {
        var products = Load().Products;
        Assert.Equal(products.Count, products.Select(p => p.SerialNumber)
            .Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void SeedFile_PassesAllValidations() =>
        Assert.Empty(SeedCatalogValidator.Validate(Load()));

    [Fact]
    public void Validator_RejectsNegativePrice()
    {
        var catalog = Load();
        var modified = catalog with { Products = catalog.Products
            .Select((p, i) => i == 0 ? p with { Price = -1 } : p).ToList() };
        Assert.Contains(SeedCatalogValidator.Validate(modified),
            e => e.Contains("invalid price", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validator_RejectsNegativeStock()
    {
        var catalog = Load();
        var modified = catalog with { Products = catalog.Products
            .Select((p, i) => i == 0 ? p with { QuantityInStock = -1 } : p).ToList() };
        Assert.Contains(SeedCatalogValidator.Validate(modified),
            e => e.Contains("negative stock", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validator_RejectsDuplicateSeedKey()
    {
        var catalog = Load();
        var modified = catalog with { Products = catalog.Products
            .Select((p, i) => i == 1 ? p with { SeedKey = catalog.Products[0].SeedKey } : p).ToList() };
        Assert.Contains(SeedCatalogValidator.Validate(modified),
            e => e.Contains("Duplicate seed key", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validator_RejectsUnknownCategory()
    {
        var catalog = Load();
        var modified = catalog with { Products = catalog.Products
            .Select((p, i) => i == 0 ? p with { CategoryCode = "NOT-A-CATEGORY" } : p).ToList() };
        Assert.Contains(SeedCatalogValidator.Validate(modified),
            e => e.Contains("unknown category", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validator_RejectsMissingProductName()
    {
        var catalog = Load();
        var modified = catalog with { Products = catalog.Products
            .Select((p, i) => i == 0 ? p with { Name = "" } : p).ToList() };
        Assert.Contains(SeedCatalogValidator.Validate(modified),
            e => e.Contains("empty name", StringComparison.OrdinalIgnoreCase));
    }
}
