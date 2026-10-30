using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OnlineStore.Catalog;
using OnlineStore.Data;
using OnlineStore.Models;

namespace OnlineStore.Tests;

public class CatalogTests
{
    [Fact]
    public async Task Catalog_GroupsActiveProductsAndPreservesEmptyCategories()
    {
        using var factory = new CatalogApplication();
        using var client = factory.CreateClient();
        await factory.SeedAsync(
            new Category { Id = 2, Name = "Tablets" },
            new Category
            {
                Id = 1, Name = "Phones", Description = "Smartphones",
                Products = new List<Product>
                {
                    new() { Id = 3, Sku = "HIDDEN", Name = "Hidden", Price = 99, IsActive = false },
                    new() { Id = 2, Sku = "S25", Name = "Samsung Galaxy S25", Price = 48000, StockQuantity = 0 },
                    new() { Id = 1, Sku = "PIXEL", Name = "Google Pixel 9", Description = "A phone", Price = 43000.50m,
                        StockQuantity = 12, ImageUrl = "/images/pixel.jpg" }
                }
            });

        var response = await client.GetAsync("/api/catalog");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var categories = (await response.Content.ReadFromJsonAsync<List<CategoryListing>>())!;
        Assert.Equal(new[] { "Phones", "Tablets" }, categories.Select(category => category.Name));
        Assert.Empty(categories[1].Products);
        var phones = categories[0];
        Assert.Equal(1, phones.Id);
        Assert.Equal("Smartphones", phones.Description);
        Assert.Equal(new[] { "Google Pixel 9", "Samsung Galaxy S25" }, phones.Products.Select(product => product.Name));
        Assert.Equal(43000.50m, phones.Products[0].Price);
        Assert.Equal(12, phones.Products[0].StockQuantity);
        Assert.Equal("/images/pixel.jpg", phones.Products[0].ImageUrl);
        Assert.Equal("A phone", phones.Products[0].Description);
        Assert.Equal(0, phones.Products[1].StockQuantity);
        Assert.Null(phones.Products[1].ImageUrl);
    }

    [Fact]
    public async Task Catalog_EmptyDatabaseReturnsEmptyArray()
    {
        using var factory = new CatalogApplication();
        using var client = factory.CreateClient();
        var categories = await client.GetFromJsonAsync<List<CategoryListing>>("/api/catalog");
        Assert.NotNull(categories);
        Assert.Empty(categories);
    }

    [Fact]
    public async Task Catalog_DoesNotExposeInventoryEntitiesOrInternalFields()
    {
        using var factory = new CatalogApplication();
        using var client = factory.CreateClient();
        await factory.SeedAsync(new Category
        {
            Name = "Phones",
            Products = new List<Product> { new() { Sku = "INTERNAL-SKU", Name = "Phone", Price = 1 } }
        });
        var json = await client.GetStringAsync("/api/catalog");
        Assert.DoesNotContain("sku", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("orderItems", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("isActive", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Catalog_UnreachableDatabaseReturnsSafeRetryableError()
    {
        using var factory = new CatalogApplication(useDatabase: true);
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/catalog");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("The catalog is temporarily unavailable.", body);
        Assert.DoesNotContain("Password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MySql", body, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/", "Browse products by category")]
    [InlineData("/products.html", "Shop by category")]
    [InlineData("/js/products.js", "/api/catalog")]
    [InlineData("/css/products.css", ".product-grid")]
    public async Task Catalog_PageAndAssetsAreServed(string path, string expected)
    {
        using var factory = new CatalogApplication();
        using var client = factory.CreateClient();
        Assert.Contains(expected, await client.GetStringAsync(path));
    }

    private sealed class CatalogApplication(bool useDatabase = false) : WebApplicationFactory<Program>
    {
        private readonly string databaseName = Guid.NewGuid().ToString();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // A closed local port exercises the MySQL error path without touching a real database.
            builder.UseSetting("ConnectionStrings:DefaultConnection",
                "Server=127.0.0.1;Port=1;Database=catalog_test;User ID=test;Password=test;Connection Timeout=1;Pooling=false");
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
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<StoreDbContext>();
            db.Categories.AddRange(categories);
            await db.SaveChangesAsync();
        }
    }
}
