using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OnlineStore.Data;
using OnlineStore.Dtos;
using OnlineStore.Models;

namespace OnlineStore.Tests;

public class ProductEndpointsTests
{
    private static Category[] SampleCatalog() => new[]
    {
        new Category
        {
            Id = 1, Name = "Phones", Description = "Smartphones",
            Products = new List<Product>
            {
                new() { Id = 1, Sku = "HIDDEN", Name = "Hidden", Price = 99, IsActive = false },
                new() { Id = 2, Sku = "S25", Name = "Samsung Galaxy S25", Price = 48000, StockQuantity = 0 },
                new() { Id = 3, Sku = "PIXEL", Name = "Google Pixel 9", Description = "A smartphone", Price = 43000.50m,
                    StockQuantity = 12, ImageUrl = "/images/pixel.jpg" }
            }
        },
        new Category
        {
            Id = 2, Name = "Tablets",
            Products = new List<Product>
            {
                new() { Id = 4, Sku = "TAB", Name = "Test Tablet", Price = 20000, StockQuantity = 3 }
            }
        }
    };

    [Fact]
    public async Task Products_ReturnsActiveProductsSortedByName_IncludingOutOfStock()
    {
        using var factory = new ProductApplication();
        using var client = factory.CreateClient();
        await factory.SeedAsync(SampleCatalog());

        var products = await client.GetFromJsonAsync<List<ProductListItemDto>>("/api/products");

        Assert.NotNull(products);
        // "Hidden" is inactive, so it must not appear.
        Assert.Equal(new[] { "Google Pixel 9", "Samsung Galaxy S25", "Test Tablet" },
            products.Select(product => product.Name));
        Assert.True(products[0].InStock);
        Assert.False(products[1].InStock);   // out of stock is listed but flagged
        Assert.Equal("Phones", products[0].CategoryName);
        Assert.Equal(43000.50m, products[0].Price);
        Assert.Equal("A smartphone", products[0].Description);
    }

    [Fact]
    public async Task Products_WithCategoryId_ReturnsOnlyThatCategory()
    {
        using var factory = new ProductApplication();
        using var client = factory.CreateClient();
        await factory.SeedAsync(SampleCatalog());

        var products = await client.GetFromJsonAsync<List<ProductListItemDto>>("/api/products?categoryId=2");

        Assert.NotNull(products);
        var tablet = Assert.Single(products);
        Assert.Equal("Test Tablet", tablet.Name);
    }

    [Fact]
    public async Task Products_WithUnknownCategoryId_ReturnsEmptyArray()
    {
        using var factory = new ProductApplication();
        using var client = factory.CreateClient();
        await factory.SeedAsync(SampleCatalog());

        var products = await client.GetFromJsonAsync<List<ProductListItemDto>>("/api/products?categoryId=999");

        Assert.NotNull(products);
        Assert.Empty(products);
    }

    [Fact]
    public async Task Products_DoesNotExposeInternalFields()
    {
        using var factory = new ProductApplication();
        using var client = factory.CreateClient();
        await factory.SeedAsync(SampleCatalog());

        var json = await client.GetStringAsync("/api/products");

        Assert.DoesNotContain("sku", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("isActive", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("orderItems", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Products_UnreachableDatabase_ReturnsSafeError()
    {
        using var factory = new ProductApplication(useDatabase: true);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/products");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MySql", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Categories_ReturnsAllCategoriesSortedByName()
    {
        using var factory = new ProductApplication();
        using var client = factory.CreateClient();
        await factory.SeedAsync(SampleCatalog());

        var categories = await client.GetFromJsonAsync<List<CategoryDto>>("/api/categories");

        Assert.NotNull(categories);
        Assert.Equal(new[] { "Phones", "Tablets" }, categories.Select(category => category.Name));
        Assert.Equal("Smartphones", categories[0].Description);
    }

    [Fact]
    public async Task Categories_UnreachableDatabase_ReturnsSafeError()
    {
        using var factory = new ProductApplication(useDatabase: true);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/categories");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    private static Category[] DetailCatalog() => new[]
    {
        new Category
        {
            Id = 1, Name = "Phones",
            Products = new List<Product>
            {
                new() { Id = 20, Sku = "PIXEL-9", Name = "Google Pixel 9",
                    Description = "A smartphone", Price = 43000.50m, StockQuantity = 12, Cost = 20000 },
                new() { Id = 21, Sku = "SOLD-OUT", Name = "Sold Out Phone", Price = 10, StockQuantity = 0 },
                new() { Id = 22, Sku = "HIDDEN", Name = "Hidden Phone", Price = 10, StockQuantity = 5, IsActive = false }
            }
        }
    };

    [Fact]
    public async Task ProductDetail_ReturnsTheCustomerFields()
    {
        using var factory = new ProductApplication();
        using var client = factory.CreateClient();
        await factory.SeedAsync(DetailCatalog());

        var product = await client.GetFromJsonAsync<ProductDetailDto>("/api/products/20");

        Assert.NotNull(product);
        Assert.Equal("Google Pixel 9", product.Name);
        Assert.Equal("Fixture model", product.Model);
        Assert.Equal("A smartphone", product.Description);
        Assert.Equal(43000.50m, product.Price);
        Assert.Equal(12, product.StockQuantity);
        Assert.True(product.InStock);
        Assert.Equal(1, product.CategoryId);
        Assert.Equal("Phones", product.CategoryName);
        Assert.Equal("Test warranty", product.WarrantyStatus);
        Assert.Equal("Test distributor", product.DistributorInformation);
    }

    [Fact]
    public async Task ProductDetail_DoesNotExposeInternalFields()
    {
        using var factory = new ProductApplication();
        using var client = factory.CreateClient();
        await factory.SeedAsync(DetailCatalog());

        var json = await client.GetStringAsync("/api/products/20");

        Assert.DoesNotContain("serialNumber", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("FIXTURE-PIXEL-9", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sku", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cost", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("isActive", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProductDetail_OutOfStockProduct_IsStillReturned()
    {
        using var factory = new ProductApplication();
        using var client = factory.CreateClient();
        await factory.SeedAsync(DetailCatalog());

        var product = await client.GetFromJsonAsync<ProductDetailDto>("/api/products/21");

        Assert.NotNull(product);
        Assert.Equal(0, product.StockQuantity);
        Assert.False(product.InStock);
    }

    [Theory]
    [InlineData("/api/products/22")]    // inactive product
    [InlineData("/api/products/999")]   // unknown id
    public async Task ProductDetail_UnknownOrInactiveProduct_Returns404(string path)
    {
        using var factory = new ProductApplication();
        using var client = factory.CreateClient();
        await factory.SeedAsync(DetailCatalog());

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ProductDetail_UnreachableDatabase_ReturnsSafeError()
    {
        using var factory = new ProductApplication(useDatabase: true);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/products/1");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MySql", body, StringComparison.OrdinalIgnoreCase);
    }

    // Starts the real app in memory. Without useDatabase, MySQL is replaced by an
    // in-memory database; with it, the app talks to a closed port to test the error path.
    private sealed class ProductApplication(bool useDatabase = false) : WebApplicationFactory<Program>
    {
        private readonly string databaseName = Guid.NewGuid().ToString();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("ConnectionStrings:DefaultConnection",
                "Server=127.0.0.1;Port=1;Database=product_test;User ID=test;Password=test;Connection Timeout=1;Pooling=false");
            builder.ConfigureServices(services =>
            {
                if (useDatabase) return;
                services.RemoveAll<StoreDbContext>();
                services.AddScoped(_ => new StoreDbContext(
                    new DbContextOptionsBuilder<StoreDbContext>().UseInMemoryDatabase(databaseName).Options));
            });
        }

        public async Task SeedAsync(params Category[] categories)
        {
            CatalogFixture.CompleteDetails(categories);
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<StoreDbContext>();
            db.Categories.AddRange(categories);
            await db.SaveChangesAsync();
        }
    }
}
