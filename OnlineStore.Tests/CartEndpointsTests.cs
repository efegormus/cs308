using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OnlineStore.Accounts;
using OnlineStore.Data;
using OnlineStore.Dtos;
using OnlineStore.Models;
using OnlineStore.Shopping;

namespace OnlineStore.Tests;

public partial class CartEndpointsTests
{
    private const string Password = "A long test passphrase!";

    private static Category[] Catalog() => new[]
    {
        new Category
        {
            Id = 1, Name = "Phones",
            Products = new List<Product>
            {
                new() { Id = 10, Sku = "P-10", Name = "Phone Ten", Price = 100.50m, StockQuantity = 5 },
                new() { Id = 11, Sku = "P-11", Name = "Phone Eleven", Price = 20m, StockQuantity = 1 },
                new() { Id = 12, Sku = "SOLD-OUT", Name = "Sold Out Phone", Price = 10m, StockQuantity = 0 },
                new() { Id = 13, Sku = "HIDDEN", Name = "Hidden Phone", Price = 10m, StockQuantity = 5, IsActive = false }
            }
        }
    };

    [Fact]
    public async Task GetCart_NewGuest_IsEmptyAndDoesNotCreateCartOrCookie()
    {
        using var factory = new CartApplication();
        await factory.SeedAsync(Catalog());
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/cart");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cart = (await response.Content.ReadFromJsonAsync<CartDto>())!;
        Assert.Empty(cart.Items);
        Assert.Equal(0, cart.ItemCount);
        Assert.Equal(0m, cart.Total);
        Assert.False(response.Headers.Contains("Set-Cookie"));
        Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
        Assert.Empty(await factory.QueryAsync(db => db.Carts.ToListAsync()));
    }

    [Fact]
    public async Task AddItem_Guest_CreatesCartWithHttpOnlyCookieAndStoresOnlyTheTokenHash()
    {
        using var factory = new CartApplication();
        await factory.SeedAsync(Catalog());
        using var client = factory.CreateClient();

        var response = await PostAsync(client, "/api/cart/items", new { productId = 10, quantity = 2 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cart = (await response.Content.ReadFromJsonAsync<CartDto>())!;
        var line = Assert.Single(cart.Items);
        Assert.Equal(10, line.ProductId);
        Assert.Equal("Phone Ten", line.Name);
        Assert.Equal(2, line.Quantity);
        Assert.Equal(201.00m, line.LineTotal);
        Assert.True(line.Available);
        Assert.Equal(2, cart.ItemCount);
        Assert.Equal(201.00m, cart.Total);

        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith(CartEndpoints.GuestCookie + "="));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        var token = cookie.Split(';')[0].Split('=')[1];

        var stored = Assert.Single(await factory.QueryAsync(db => db.Carts.ToListAsync()));
        Assert.Null(stored.UserId);
        Assert.NotNull(stored.GuestTokenHash);
        Assert.NotEqual(token, stored.GuestTokenHash);

        // The cookie brings the cart back on the next request.
        var again = (await client.GetFromJsonAsync<CartDto>("/api/cart"))!;
        Assert.Equal(2, again.ItemCount);
    }

    [Fact]
    public async Task AddItem_SameProductTwice_IncreasesTheQuantityOfOneLine()
    {
        using var factory = new CartApplication();
        await factory.SeedAsync(Catalog());
        using var client = factory.CreateClient();

        await PostAsync(client, "/api/cart/items", new { productId = 10, quantity = 1 });
        var response = await PostAsync(client, "/api/cart/items", new { productId = 10, quantity = 3 });

        var cart = (await response.Content.ReadFromJsonAsync<CartDto>())!;
        var line = Assert.Single(cart.Items);
        Assert.Equal(4, line.Quantity);
        Assert.Single(await factory.QueryAsync(db => db.CartItems.ToListAsync()));
    }

    [Fact]
    public async Task AddItem_MoreThanStock_Returns409AndLeavesTheCartUnchanged()
    {
        using var factory = new CartApplication();
        await factory.SeedAsync(Catalog());
        using var client = factory.CreateClient();
        await PostAsync(client, "/api/cart/items", new { productId = 11, quantity = 1 });

        var response = await PostAsync(client, "/api/cart/items", new { productId = 11, quantity = 1 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var cart = (await client.GetFromJsonAsync<CartDto>("/api/cart"))!;
        Assert.Equal(1, Assert.Single(cart.Items).Quantity);
    }

    [Fact]
    public async Task AddItem_OutOfStockProduct_Returns409AndCreatesNoCart()
    {
        using var factory = new CartApplication();
        await factory.SeedAsync(Catalog());
        using var client = factory.CreateClient();

        var response = await PostAsync(client, "/api/cart/items", new { productId = 12, quantity = 1 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        Assert.Empty(await factory.QueryAsync(db => db.Carts.ToListAsync()));
    }

    [Theory]
    [InlineData(13)]   // inactive product
    [InlineData(999)]  // unknown product
    public async Task AddItem_UnknownOrInactiveProduct_Returns404(long productId)
    {
        using var factory = new CartApplication();
        await factory.SeedAsync(Catalog());
        using var client = factory.CreateClient();

        var response = await PostAsync(client, "/api/cart/items", new { productId, quantity = 1 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(10, 0)]
    [InlineData(10, -1)]
    [InlineData(10, 100)]
    [InlineData(0, 1)]
    public async Task AddItem_InvalidInput_Returns400(long productId, int quantity)
    {
        using var factory = new CartApplication();
        await factory.SeedAsync(Catalog());
        using var client = factory.CreateClient();

        var response = await PostAsync(client, "/api/cart/items", new { productId, quantity });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddItem_WithoutCsrfToken_Returns400()
    {
        using var factory = new CartApplication();
        await factory.SeedAsync(Catalog());
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/cart/items", new { productId = 10, quantity = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await factory.QueryAsync(db => db.Carts.ToListAsync()));
    }

    [Fact]
    public async Task RemoveItem_RemovesTheLineAndIsSafeToRepeat()
    {
        using var factory = new CartApplication();
        await factory.SeedAsync(Catalog());
        using var client = factory.CreateClient();
        await PostAsync(client, "/api/cart/items", new { productId = 10, quantity = 1 });
        await PostAsync(client, "/api/cart/items", new { productId = 11, quantity = 1 });

        var first = await SendAsync(client, HttpMethod.Delete, "/api/cart/items/10");
        var second = await SendAsync(client, HttpMethod.Delete, "/api/cart/items/10");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var cart = (await second.Content.ReadFromJsonAsync<CartDto>())!;
        Assert.Equal(11, Assert.Single(cart.Items).ProductId);
    }

    [Fact]
    public async Task RemoveItem_WithoutCsrfToken_Returns400()
    {
        using var factory = new CartApplication();
        await factory.SeedAsync(Catalog());
        using var client = factory.CreateClient();
        await PostAsync(client, "/api/cart/items", new { productId = 10, quantity = 1 });

        var response = await client.DeleteAsync("/api/cart/items/10");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(1, (await client.GetFromJsonAsync<CartDto>("/api/cart"))!.ItemCount);
    }

    [Fact]
    public async Task TwoGuests_HaveSeparateCarts()
    {
        using var factory = new CartApplication();
        await factory.SeedAsync(Catalog());
        using var first = factory.CreateClient();
        using var second = factory.CreateClient();

        await PostAsync(first, "/api/cart/items", new { productId = 10, quantity = 2 });

        Assert.Equal(2, (await first.GetFromJsonAsync<CartDto>("/api/cart"))!.ItemCount);
        Assert.Equal(0, (await second.GetFromJsonAsync<CartDto>("/api/cart"))!.ItemCount);
    }

    [Fact]
    public async Task TamperedGuestCookie_IsIgnored()
    {
        using var factory = new CartApplication();
        await factory.SeedAsync(Catalog());
        using var client = factory.CreateClient();
        await PostAsync(client, "/api/cart/items", new { productId = 10, quantity = 1 });

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/cart");
        request.Headers.Add("Cookie", CartEndpoints.GuestCookie + "=not-a-real-token");
        using var stranger = factory.CreateClient();
        var response = await stranger.SendAsync(request);

        Assert.Equal(0, (await response.Content.ReadFromJsonAsync<CartDto>())!.ItemCount);
    }

    [Fact]
    public async Task Customer_HasAnAccountCart_NotAGuestCart()
    {
        using var factory = new CartApplication();
        await factory.SeedAsync(Catalog());
        using var client = factory.CreateClient();
        await LoginNewCustomerAsync(client);

        var response = await PostAsync(client, "/api/cart/items", new { productId = 10, quantity = 2 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cookies = response.Headers.TryGetValues("Set-Cookie", out var values) ? values : Array.Empty<string>();
        Assert.DoesNotContain(cookies, value => value.StartsWith(CartEndpoints.GuestCookie + "="));
        var stored = Assert.Single(await factory.QueryAsync(db => db.Carts.ToListAsync()));
        Assert.NotNull(stored.UserId);
        Assert.Null(stored.GuestTokenHash);
        Assert.Equal(2, (await client.GetFromJsonAsync<CartDto>("/api/cart"))!.ItemCount);

        // Someone who is not logged in cannot see that cart.
        using var guest = factory.CreateClient();
        Assert.Equal(0, (await guest.GetFromJsonAsync<CartDto>("/api/cart"))!.ItemCount);
    }

    [Fact]
    public async Task Customer_WithTwoOpenCarts_GetsAMergeRequiredConflict()
    {
        using var factory = new CartApplication();
        await factory.SeedAsync(Catalog());
        using var client = factory.CreateClient();
        var userId = await LoginNewCustomerAsync(client);
        await factory.UpdateAsync(async db =>
        {
            db.Carts.AddRange(new Cart { UserId = userId }, new Cart { UserId = userId });
            await db.SaveChangesAsync();
        });

        Assert.Equal(HttpStatusCode.Conflict, (await client.GetAsync("/api/cart")).StatusCode);
        var add = await PostAsync(client, "/api/cart/items", new { productId = 10, quantity = 1 });
        Assert.Equal(HttpStatusCode.Conflict, add.StatusCode);
        Assert.Empty(await factory.QueryAsync(db => db.CartItems.ToListAsync()));
    }

    [Fact]
    public async Task AddItem_ToACartWithMaxLines_Returns409ButStillAllowsMoreUnitsOfAnExistingLine()
    {
        using var factory = new CartApplication();
        await factory.SeedAsync(new Category
        {
            Id = 2, Name = "Many",
            Products = Enumerable.Range(100, CartEndpoints.MaxLines + 1)
                .Select(id => new Product { Id = id, Sku = $"M-{id}", Name = $"Many {id}", Price = 1m, StockQuantity = 5 })
                .ToList()
        });
        using var client = factory.CreateClient();
        await PostAsync(client, "/api/cart/items", new { productId = 100, quantity = 1 });
        await factory.UpdateAsync(async db =>
        {
            var cartId = (await db.Carts.SingleAsync()).Id;
            db.CartItems.AddRange(Enumerable.Range(101, CartEndpoints.MaxLines - 1)
                .Select(id => new CartItem { CartId = cartId, ProductId = id, Quantity = 1 }));
            await db.SaveChangesAsync();
        });

        var newLine = await PostAsync(client, "/api/cart/items", new { productId = 200, quantity = 1 });
        var moreUnits = await PostAsync(client, "/api/cart/items", new { productId = 100, quantity = 1 });

        Assert.Equal(HttpStatusCode.Conflict, newLine.StatusCode);
        Assert.Equal(HttpStatusCode.OK, moreUnits.StatusCode);
    }

    [Fact]
    public async Task Cart_ReportsLinesThatNoLongerHaveEnoughStock()
    {
        using var factory = new CartApplication();
        await factory.SeedAsync(Catalog());
        using var client = factory.CreateClient();
        await PostAsync(client, "/api/cart/items", new { productId = 10, quantity = 4 });
        await factory.UpdateAsync(async db =>
        {
            var product = await db.Products.SingleAsync(p => p.Id == 10);
            product.StockQuantity = 2;
            await db.SaveChangesAsync();
        });

        var cart = (await client.GetFromJsonAsync<CartDto>("/api/cart"))!;

        var line = Assert.Single(cart.Items);
        Assert.False(line.Available);
        Assert.Equal(2, line.StockQuantity);
    }

    [Fact]
    public async Task Cart_UnreachableDatabase_ReturnsSafeError()
    {
        using var factory = new CartApplication(useDatabase: true);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/cart");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode); // a guest without a cookie never touches the database
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/cart");
        request.Headers.Add("Cookie", CartEndpoints.GuestCookie + "=" + new string('a', 64));
        var failing = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, failing.StatusCode);
        var body = await failing.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MySql", body, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> CsrfTokenAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf")).GetProperty("token").GetString()!;

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", await CsrfTokenAsync(client));
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-CSRF-TOKEN", await CsrfTokenAsync(client));
        return await client.SendAsync(request);
    }

    private static async Task<long> LoginNewCustomerAsync(HttpClient client)
    {
        var register = await PostAsync(client, "/api/auth/register",
            new RegisterRequest("Cart Customer", "cart@example.com", "TAX-1", "1 Example Street", Password));
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var userId = (await register.Content.ReadFromJsonAsync<AccountDto>())!.Id;
        var login = await PostAsync(client, "/api/auth/login", new LoginRequest("cart@example.com", Password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return userId;
    }

    // Starts the real app in memory. Without useDatabase, MySQL is replaced by an in-memory database.
    private sealed class CartApplication(bool useDatabase = false) : WebApplicationFactory<Program>
    {
        private readonly string databaseName = Guid.NewGuid().ToString();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:DefaultConnection",
                "Server=127.0.0.1;Port=1;Database=cart_test;User ID=test;Password=test;Connection Timeout=1;Pooling=false");
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

        public async Task<T> QueryAsync<T>(Func<StoreDbContext, Task<T>> query)
        {
            using var scope = Services.CreateScope();
            return await query(scope.ServiceProvider.GetRequiredService<StoreDbContext>());
        }

        public async Task UpdateAsync(Func<StoreDbContext, Task> change)
        {
            using var scope = Services.CreateScope();
            await change(scope.ServiceProvider.GetRequiredService<StoreDbContext>());
        }
    }
}
