using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OnlineStore.Data;
using OnlineStore.Catalog;
using OnlineStore.Dtos;
using OnlineStore.Models;

namespace OnlineStore.Tests;

// SCRUM-50 (search) and SCRUM-51 (sorting) on GET /api/products.
public class ProductSearchSortTests
{
    private static Category[] SampleCatalog() => new[]
    {
        new Category
        {
            Id = 1, Name = "Phones",
            Products = new List<Product>
            {
                new() { Id = 1, Sku = "HIDDEN", Name = "Hidden Pixel", Description = "Draft", Price = 99, IsActive = false },
                new() { Id = 2, Sku = "S25", Name = "Samsung Galaxy S25", Description = "Flagship handset", Price = 48000, StockQuantity = 0 },
                new() { Id = 3, Sku = "PIXEL", Name = "Google Pixel 9", Description = "A smartphone", Price = 43000.50m,
                    StockQuantity = 12 },
                new() { Id = 5, Sku = "ALPHA", Name = "Alpha Zero", Description = "Sale 100% off_today", Price = 20000,
                    StockQuantity = 4 }
            }
        },
        new Category
        {
            Id = 2, Name = "Tablets",
            Products = new List<Product>
            {
                new() { Id = 4, Sku = "TAB", Name = "Test Tablet", Description = "Large screen device", Price = 20000, StockQuantity = 3 }
            }
        }
    };

    private static async Task<string[]> NamesAsync(HttpClient client, string query)
    {
        var products = await client.GetFromJsonAsync<List<ProductListItemDto>>("/api/products" + query);
        Assert.NotNull(products);
        return products.Select(product => product.Name).ToArray();
    }

    [Fact]
    public async Task Search_MatchesNameIgnoringCase()
    {
        using var factory = new SearchApplication();
        using var client = factory.CreateClient();
        await factory.SeedAsync(SampleCatalog());

        Assert.Equal(new[] { "Google Pixel 9" }, await NamesAsync(client, "?search=PIXEL"));
    }

    [Fact]
    public async Task Search_MatchesDescription_AndIgnoresInactiveProducts()
    {
        using var factory = new SearchApplication();
        using var client = factory.CreateClient();
        await factory.SeedAsync(SampleCatalog());

        Assert.Equal(new[] { "Google Pixel 9" }, await NamesAsync(client, "?search=smartphone"));
        // The inactive "Hidden Pixel" must not appear for "pixel".
        Assert.DoesNotContain("Hidden Pixel", await NamesAsync(client, "?search=pixel"));
    }

    [Fact]
    public async Task Search_KeepsOutOfStockProductsInTheResults()
    {
        using var factory = new SearchApplication();
        using var client = factory.CreateClient();
        await factory.SeedAsync(SampleCatalog());

        var products = await client.GetFromJsonAsync<List<ProductListItemDto>>("/api/products?search=galaxy");

        Assert.NotNull(products);
        var galaxy = Assert.Single(products);
        Assert.Equal(0, galaxy.StockQuantity);
        Assert.False(galaxy.InStock);
    }

    [Theory]
    [InlineData("")]
    [InlineData("%20%20")]
    public async Task Search_EmptyOrBlank_IsTreatedAsNoSearch(string value)
    {
        using var factory = new SearchApplication();
        using var client = factory.CreateClient();
        await factory.SeedAsync(SampleCatalog());

        Assert.Equal(4, (await NamesAsync(client, "?search=" + value)).Length);
    }

    [Fact]
    public async Task Search_NoMatch_ReturnsEmptyArray()
    {
        using var factory = new SearchApplication();
        using var client = factory.CreateClient();
        await factory.SeedAsync(SampleCatalog());

        Assert.Empty(await NamesAsync(client, "?search=nothing-like-this"));
    }

    [Theory]
    [InlineData("%25", "Alpha Zero")]   // a literal "%" only matches text that contains one
    [InlineData("_", "Alpha Zero")]     // a literal "_" is not a one-character wildcard
    [InlineData("zzz%25", null)]        // "%" is not a wildcard that swallows the rest
    public async Task Search_TreatsLikeWildcardsAsPlainCharacters(string value, string? expected)
    {
        using var factory = new SearchApplication();
        using var client = factory.CreateClient();
        await factory.SeedAsync(SampleCatalog());

        var names = await NamesAsync(client, "?search=" + value);

        Assert.Equal(expected is null ? Array.Empty<string>() : new[] { expected }, names);
    }

    [Fact]
    public async Task Search_CombinesWithCategoryId()
    {
        using var factory = new SearchApplication();
        using var client = factory.CreateClient();
        await factory.SeedAsync(SampleCatalog());

        Assert.Empty(await NamesAsync(client, "?search=pixel&categoryId=2"));
        Assert.Equal(new[] { "Test Tablet" }, await NamesAsync(client, "?search=tablet&categoryId=2"));
    }

    [Fact]
    public async Task Search_LongerThan100Characters_ReturnsProblemJson400()
    {
        using var factory = new SearchApplication();
        using var client = factory.CreateClient();
        await factory.SeedAsync(SampleCatalog());

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/products?search=" + new string('a', 100))).StatusCode);
        var response = await client.GetAsync("/api/products?search=" + new string('a', 101));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Sort_ByPrice_AscendingByDefault_WithNameThenIdTieBreak()
    {
        using var factory = new SearchApplication();
        using var client = factory.CreateClient();
        await factory.SeedAsync(SampleCatalog());

        var expected = new[] { "Alpha Zero", "Test Tablet", "Google Pixel 9", "Samsung Galaxy S25" };
        Assert.Equal(expected, await NamesAsync(client, "?sort=price"));
        Assert.Equal(expected, await NamesAsync(client, "?sort=PRICE&order=asc"));
    }

    [Fact]
    public async Task Sort_ByPriceDescending_KeepsTieBreakByName()
    {
        using var factory = new SearchApplication();
        using var client = factory.CreateClient();
        await factory.SeedAsync(SampleCatalog());

        Assert.Equal(new[] { "Samsung Galaxy S25", "Google Pixel 9", "Alpha Zero", "Test Tablet" },
            await NamesAsync(client, "?sort=price&order=desc"));
    }

    [Fact]
    public async Task Sort_ByPopularity_CountsOnlyConfirmedNotCancelledOrders()
    {
        using var factory = new SearchApplication();
        using var client = factory.CreateClient();
        await factory.SeedAsync(SampleCatalog());
        await factory.AddOrdersAsync();

        // Tablet sold 5, Galaxy 2. Pixel's cancelled and pending orders do not count, so Pixel and Alpha are 0
        // and fall back to name order.
        Assert.Equal(new[] { "Test Tablet", "Samsung Galaxy S25", "Alpha Zero", "Google Pixel 9" },
            await NamesAsync(client, "?sort=popularity"));
        Assert.Equal(new[] { "Alpha Zero", "Google Pixel 9", "Samsung Galaxy S25", "Test Tablet" },
            await NamesAsync(client, "?sort=popularity&order=asc"));
    }

    [Fact]
    public async Task Sort_ByPopularity_WithoutOrders_FallsBackToNameOrder()
    {
        using var factory = new SearchApplication();
        using var client = factory.CreateClient();
        await factory.SeedAsync(SampleCatalog());

        Assert.Equal(new[] { "Alpha Zero", "Google Pixel 9", "Samsung Galaxy S25", "Test Tablet" },
            await NamesAsync(client, "?sort=popularity"));
    }

    [Fact]
    public async Task SortAndSearch_AndCategory_Combine()
    {
        using var factory = new SearchApplication();
        using var client = factory.CreateClient();
        await factory.SeedAsync(SampleCatalog());

        Assert.Equal(new[] { "Samsung Galaxy S25", "Google Pixel 9", "Alpha Zero" },
            await NamesAsync(client, "?categoryId=1&search=s&sort=price&order=desc"));
        Assert.Equal(new[] { "Alpha Zero" }, await NamesAsync(client, "?categoryId=1&search=zero&sort=price"));
    }

    [Fact]
    public async Task CombinedFilters_AndOptInPagination_ReturnDeterministicCounts()
    {
        using var factory = new SearchApplication();
        using var client = factory.CreateClient();
        await factory.SeedAsync(SampleCatalog());
        await factory.AddBrandsAsync();

        var combined = await client.GetFromJsonAsync<List<ProductListItemDto>>(
        "/api/products?categoryId=1&brandId=1&minPrice=20000&maxPrice=45000&search=smartphone");
        Assert.NotNull(combined);
        Assert.Equal("Google Pixel 9", Assert.Single(combined).Name);
        Assert.Equal("Google", combined[0].BrandName);
        var brands = await client.GetFromJsonAsync<List<BrandListing>>("/api/brands");
        Assert.NotNull(brands);
        Assert.Equal("Google", Assert.Single(brands).Name);

        var first = await client.GetFromJsonAsync<ProductPageDto>("/api/products?sort=price&page=1&pageSize=2");
        var second = await client.GetFromJsonAsync<ProductPageDto>("/api/products?sort=price&page=2&pageSize=2");
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(4, first.TotalCount);
        Assert.Equal(2, first.TotalPages);
        Assert.Equal(new[] { "Alpha Zero", "Test Tablet" }, first.Items.Select(item => item.Name));
        Assert.Equal(new[] { "Google Pixel 9", "Samsung Galaxy S25" }, second.Items.Select(item => item.Name));

        var legacy = await client.GetAsync("/api/products");
        Assert.Equal("application/json", legacy.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("[", await legacy.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?pageSize=101")]
    [InlineData("?minPrice=20&maxPrice=10")]
    [InlineData("?brandId=0")]
    public async Task InvalidCatalogRangesAndPagination_ReturnProblemJson(string query)
    {
        using var factory = new SearchApplication();
        using var client = factory.CreateClient();
        await factory.SeedAsync(SampleCatalog());

        using var response = await client.GetAsync("/api/products" + query);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("?sort=rating")]
    [InlineData("?sort=price&order=sideways")]
    [InlineData("?order=up")]
    public async Task InvalidSortOrOrder_ReturnsProblemJson400(string query)
    {
        using var factory = new SearchApplication();
        using var client = factory.CreateClient();
        await factory.SeedAsync(SampleCatalog());

        var response = await client.GetAsync("/api/products" + query);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Products_WithoutParameters_StillSortsByNameThenId()
    {
        using var factory = new SearchApplication();
        using var client = factory.CreateClient();
        await factory.SeedAsync(SampleCatalog());

        Assert.Equal(new[] { "Alpha Zero", "Google Pixel 9", "Samsung Galaxy S25", "Test Tablet" },
            await NamesAsync(client, ""));
    }

    private sealed class SearchApplication : WebApplicationFactory<Program>
    {
        private readonly string databaseName = Guid.NewGuid().ToString();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("ConnectionStrings:DefaultConnection",
                "Server=127.0.0.1;Port=1;Database=product_test;User ID=test;Password=test;Connection Timeout=1;Pooling=false");
            builder.ConfigureServices(services =>
            {
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

        public async Task AddOrdersAsync()
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<StoreDbContext>();
            await PopularityOrders.AddAsync(db, topId: 4, middleId: 2, unsoldId: 3);
        }

        public async Task AddBrandsAsync()
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<StoreDbContext>();
            var brand = new Brand { Id = 1, Name = "Google" };
            db.Brands.Add(brand);
            var pixel = await db.Products.SingleAsync(product => product.Id == 3);
            pixel.Brand = brand;
            await db.SaveChangesAsync();
        }
    }
}

// Orders for the popularity tests: the top product sells 5 confirmed units, the middle one 2, and the
// unsold product only has a cancelled and a pending order, which must not count.
internal static class PopularityOrders
{
    public static async Task AddAsync(StoreDbContext db, long topId, long middleId, long unsoldId)
    {
        var user = new User { Name = "Buyer", Email = "buyer@example.com", PasswordHash = "x" };
        db.Users.Add(user);
        var products = await db.Products.ToDictionaryAsync(product => product.Id);
        var now = DateTime.UtcNow;

        Order Confirmed(params (long ProductId, int Quantity)[] lines)
        {
            var order = new Order { User = user }.WithCustomerSnapshot(user);
            Assert.True(order.ConfirmPayment(now));
            foreach (var (productId, quantity) in lines)
                order.OrderItems.Add(OrderItem.Snapshot(products[productId], quantity));
            return order;
        }

        var first = Confirmed((topId, 3));
        var second = Confirmed((topId, 2), (middleId, 2));
        var cancelled = Confirmed((unsoldId, 100));
        Assert.True(cancelled.TransitionTo(OrderStatus.Cancelled, now));
        var pending = new Order { User = user }.WithCustomerSnapshot(user);
        pending.OrderItems.Add(OrderItem.Snapshot(products[unsoldId], 50));

        db.Orders.AddRange(first, second, cancelled, pending);
        await db.SaveChangesAsync();
    }
}

// The popularity ranking and the escaped search must also work on real MySQL, where they become SQL.
[Collection("MySql")]
public class ProductSearchSortMySqlTests(MySqlFixture fixture) : IClassFixture<MySqlFixture>
{
    [MySqlFact]
    public async Task MySql_CombinedFiltersPaginationAndProductImages_UseRedesignedSchema()
    {
        await fixture.ResetAsync();
        long brandId, expectedProductId;
        await using (var db = fixture.Create())
        {
            var category = new Category { Name = "Phones" };
            var brand = new Brand { Name = "Google" };
            var first = NewProduct("GOOGLE-A", "Pixel Phone", "Smartphone");
            first.Price = 200;
            first.Brand = brand;
            first.Images.Add(new ProductImage { StorageKey = "/images/pixel-front.jpg", AltText = "Front", IsPrimary = true, SortOrder = 0 });
            var second = NewProduct("GOOGLE-B", "Pixel Pro Phone", "Smartphone");
            second.Price = 300;
            second.Brand = brand;
            second.Images.Add(new ProductImage { StorageKey = "/images/pixel-pro.jpg", AltText = "Pro", IsPrimary = true, SortOrder = 0 });
            var third = NewProduct("OTHER", "Tablet", "Large screen");
            third.Price = 250;
            category.Products.Add(first);
            category.Products.Add(second);
            category.Products.Add(third);
            db.Brands.Add(brand);
            db.Categories.Add(category);
            await db.SaveChangesAsync();
            (brandId, expectedProductId) = (brand.Id, second.Id);
        }

        using var factory = new MySqlSearchApplication(fixture);
        using var client = factory.CreateClient();
        var page = await client.GetFromJsonAsync<ProductPageDto>(
            $"/api/products?brandId={brandId}&minPrice=100&maxPrice=350&search=smartphone&sort=price&order=desc&page=1&pageSize=1");
        Assert.NotNull(page);
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(2, page.TotalPages);
        Assert.Equal("Pixel Pro Phone", Assert.Single(page.Items).Name);

        var detail = await client.GetFromJsonAsync<ProductDetailDto>($"/api/products/{expectedProductId}");
        Assert.NotNull(detail);
        Assert.Equal("Google", detail.BrandName);
        Assert.Equal("/images/pixel-pro.jpg", Assert.Single(detail.Images!).Url);

        using var legacy = await client.GetAsync("/api/products");
        Assert.StartsWith("[", await legacy.Content.ReadAsStringAsync());
    }

    [MySqlFact]
    public async Task MySql_PopularityAndSearch_CountConfirmedOrdersAndEscapeWildcards()
    {
        await fixture.ResetAsync();
        long alphaId, betaId, gammaId;
        await using (var db = fixture.Create())
        {
            var category = new Category { Name = "Phones" };
            var alpha = NewProduct("A", "Alpha 50% Phone", "First");
            var beta = NewProduct("B", "Beta Phone", "Second");
            var gamma = NewProduct("C", "Gamma Phone", "Third");
            category.Products.Add(alpha);
            category.Products.Add(beta);
            category.Products.Add(gamma);
            db.Categories.Add(category);
            await db.SaveChangesAsync();
            (alphaId, betaId, gammaId) = (alpha.Id, beta.Id, gamma.Id);
            await PopularityOrders.AddAsync(db, topId: gammaId, middleId: betaId, unsoldId: alphaId);
        }

        using var factory = new MySqlSearchApplication(fixture);
        using var client = factory.CreateClient();

        var popular = await client.GetFromJsonAsync<List<ProductListItemDto>>("/api/products?sort=popularity");
        Assert.NotNull(popular);
        // Gamma sold 5, Beta 2; Alpha only has a cancelled and a pending order, so it counts 0.
        Assert.Equal(new[] { "Gamma Phone", "Beta Phone", "Alpha 50% Phone" }, popular.Select(product => product.Name));

        var percent = await client.GetFromJsonAsync<List<ProductListItemDto>>("/api/products?search=50%25");
        Assert.NotNull(percent);
        Assert.Equal("Alpha 50% Phone", Assert.Single(percent).Name);
        var wildcard = await client.GetFromJsonAsync<List<ProductListItemDto>>("/api/products?search=%25hone");
        Assert.NotNull(wildcard);
        Assert.Empty(wildcard);
    }

    private static Product NewProduct(string sku, string name, string description)
    {
        var product = new Product { Sku = sku, Name = name, Price = 100, StockQuantity = 5 };
        CatalogFixture.CompleteDetails(product);
        product.Description = description;
        return product;
    }

    private sealed class MySqlSearchApplication(MySqlFixture fixture) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("ConnectionStrings:DefaultConnection",
                "Server=127.0.0.1;Port=1;Database=product_test;User ID=test;Password=test;Connection Timeout=1;Pooling=false");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<StoreDbContext>();
                services.AddScoped(_ => fixture.Create());
            });
        }
    }
}
