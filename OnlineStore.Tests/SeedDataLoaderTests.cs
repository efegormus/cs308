using Microsoft.EntityFrameworkCore;
using OnlineStore.Data;
using OnlineStore.Models;
using OnlineStore.Seeding;

namespace OnlineStore.Tests;

// Integration tests: the loader runs against an EF Core in-memory database (no MySQL needed).
// The in-memory provider does not enforce MySQL rules such as check constraints.
public class SeedDataLoaderTests
{
    // A new context on the same named database, so tests read what was really saved.
    private static StoreDbContext OpenDb(string name) => new(
        new DbContextOptionsBuilder<StoreDbContext>().UseInMemoryDatabase(name).Options);

    private static SeedProduct Sample(string key, string categoryCode, decimal price = 1000m, int stock = 5) => new()
    {
        SeedKey = key, Name = $"Name {key}", Model = $"Model {key}", SerialNumber = $"SERIAL-{key}",
        Description = "Sample item", QuantityInStock = stock, Price = price, Cost = price / 2,
        WarrantyStatus = "Active", DistributorInformation = "Demo Distributor", CategoryCode = categoryCode
    };

    private static SeedCatalog SmallCatalog() => new()
    {
        Categories = new List<SeedCategory>
        {
            new() { Code = "PHONES", Name = "Phones" },
            new() { Code = "TABLETS", Name = "Tablets" }
        },
        Products = new List<SeedProduct>
        {
            Sample("P-1", "PHONES", price: 2000m, stock: 7),
            Sample("P-2", "PHONES"),
            Sample("T-1", "TABLETS")
        }
    };

    private static string SeedFilePath => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "SeedData", "seed-products.json"));

    [Fact]
    public async Task Load_OnEmptyDatabase_AddsAllCategoriesAndProducts()
    {
        var name = Guid.NewGuid().ToString();
        using (var db = OpenDb(name))
        {
            var result = await SeedDataLoader.LoadAsync(db, SmallCatalog());
            Assert.Equal(new SeedResult(2, 3), result);
        }

        using var check = OpenDb(name);
        Assert.Equal(2, await check.Categories.CountAsync());
        Assert.Equal(3, await check.Products.CountAsync());
    }

    [Fact]
    public async Task Load_SavesProductFieldsAndCategory()
    {
        var name = Guid.NewGuid().ToString();
        using (var db = OpenDb(name))
        {
            await SeedDataLoader.LoadAsync(db, SmallCatalog());
        }

        using var check = OpenDb(name);
        var product = await check.Products.Include(p => p.Category).SingleAsync(p => p.Sku == "P-1");
        Assert.Equal("Name P-1", product.Name);
        Assert.Equal("Model P-1", product.Model);
        Assert.Equal("SERIAL-P-1", product.SerialNumber);
        Assert.Equal(2000m, product.Price);
        Assert.Equal(1000m, product.Cost);
        Assert.Equal(7, product.StockQuantity);
        Assert.Equal("Active", product.WarrantyStatus);
        Assert.Equal("Demo Distributor", product.DistributorInformation);
        Assert.Equal("Phones", product.Category.Name);
    }

    [Fact]
    public async Task Load_RunTwice_DoesNotAddDuplicates()
    {
        var name = Guid.NewGuid().ToString();
        using (var db = OpenDb(name))
        {
            await SeedDataLoader.LoadAsync(db, SmallCatalog());
        }

        using (var db = OpenDb(name))
        {
            var second = await SeedDataLoader.LoadAsync(db, SmallCatalog());
            Assert.Equal(new SeedResult(0, 0), second);
        }

        using var check = OpenDb(name);
        Assert.Equal(2, await check.Categories.CountAsync());
        Assert.Equal(3, await check.Products.CountAsync());
    }

    [Fact]
    public async Task Load_WhenCategoryAlreadyExists_ReusesIt()
    {
        var name = Guid.NewGuid().ToString();
        using (var db = OpenDb(name))
        {
            db.Categories.Add(new Category { Name = "Phones", Description = "Already here" });
            await db.SaveChangesAsync();
            var result = await SeedDataLoader.LoadAsync(db, SmallCatalog());
            Assert.Equal(1, result.CategoriesAdded);   // only "Tablets" is new
        }

        using var check = OpenDb(name);
        var phones = await check.Categories.Include(c => c.Products).SingleAsync(c => c.Name == "Phones");
        Assert.Equal("Already here", phones.Description);
        Assert.Equal(2, phones.Products.Count);
    }

    [Fact]
    public async Task Load_WhenSkuAlreadyExists_KeepsTheExistingProduct()
    {
        var name = Guid.NewGuid().ToString();
        using (var db = OpenDb(name))
        {
            db.Products.Add(new Product
            {
                Sku = "P-1", Name = "Edited by a manager", Price = 1m, StockQuantity = 99,
                Category = new Category { Name = "Phones" }
            });
            await db.SaveChangesAsync();
            var result = await SeedDataLoader.LoadAsync(db, SmallCatalog());
            Assert.Equal(2, result.ProductsAdded);   // P-2 and T-1; P-1 is skipped
        }

        using var check = OpenDb(name);
        var product = await check.Products.SingleAsync(p => p.Sku == "P-1");
        Assert.Equal("Edited by a manager", product.Name);
        Assert.Equal(99, product.StockQuantity);
    }

    [Fact]
    public async Task Load_WithInvalidSeedData_WritesNothing()
    {
        var name = Guid.NewGuid().ToString();
        var catalog = SmallCatalog();
        catalog.Products.Add(Sample("BAD-1", "PHONES", price: 0m));   // invalid price

        using (var db = OpenDb(name))
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => SeedDataLoader.LoadAsync(db, catalog));
        }

        using var check = OpenDb(name);
        Assert.Empty(await check.Categories.ToListAsync());
        Assert.Empty(await check.Products.ToListAsync());
    }

    [Fact]
    public async Task Load_RealSeedFile_AddsTwoCategoriesAndTenProducts()
    {
        var name = Guid.NewGuid().ToString();
        using (var db = OpenDb(name))
        {
            var result = await SeedDataLoader.LoadAsync(db, SeedCatalogReader.ReadFromFile(SeedFilePath));
            Assert.Equal(new SeedResult(2, 10), result);
        }

        using var check = OpenDb(name);
        Assert.Equal(5, await check.Products.CountAsync(p => p.Category.Name == "Phones"));
        Assert.Equal(5, await check.Products.CountAsync(p => p.Category.Name == "Tablets"));
    }
}
