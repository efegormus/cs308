using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using OnlineStore.Catalog;
using OnlineStore.Data;
using OnlineStore.Models;
using OnlineStore.Purchasing;

namespace OnlineStore.Tests;

// SCRUM-77 on real MySQL: the routes, the role policy, the CSRF guard and the unique keys that decide a race.
[Collection("MySql")]
public class CatalogManagementMySqlTests(MySqlFixture fixture) : IClassFixture<MySqlFixture>
{
    private const string Root = "/api/product-management";

    private static object NewProduct(long categoryId, string sku, object? price = null) => new
    {
        categoryId, sku, name = "HTTP phone", model = "H-1", serialNumber = "SN-" + sku, description = "Made over HTTP.",
        price = price ?? 1500.50m, stockQuantity = 7, warrantyStatus = "2 years", distributorName = "HTTP Distribution",
        distributorContact = "http@example.com"
    };

    [MySqlFact]
    public async Task MySql_ManagementHttpIsProductManagerOnlyCsrfGuardedAndChangesWhatShoppersSee()
    {
        await fixture.ResetAsync(); ReviewFixture.Graph g;
        await using (var seed = fixture.Create()) g = await ReviewFixture.SeedAsync(seed);
        using var app = new ManagementApplication(fixture);
        using var guest = app.CreateClient(); using var customer = app.CreateClient(); using var manager = app.CreateClient();

        // Guests and customers cannot reach it, for reads and for changes.
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.GetAsync($"{Root}/categories")).StatusCode);
        await Login(customer, g.Buyer.Email);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync($"{Root}/categories")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(customer, HttpMethod.Post, $"{Root}/categories", new { name = "Nope" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(customer, HttpMethod.Post, $"{Root}/products", NewProduct(1, "NOPE"))).StatusCode);
        foreach (var role in new[] { UserRole.SalesManager, UserRole.SupportAgent })
        {
            await SetRole(g.Other.Id, role);
            using var wrong = app.CreateClient(); await Login(wrong, g.Other.Email);
            Assert.Equal(HttpStatusCode.Forbidden, (await wrong.GetAsync($"{Root}/products")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await Send(wrong, HttpMethod.Delete, $"{Root}/products/{g.Product.Id}")).StatusCode);
        }

        await SetRole(g.Other.Id, UserRole.ProductManager);
        await Login(manager, g.Other.Email);
        // A change without the CSRF token, with an unknown field or with bad values is refused.
        Assert.Equal(HttpStatusCode.BadRequest, (await manager.PostAsJsonAsync($"{Root}/categories", new { name = "No token" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(manager, HttpMethod.Post, $"{Root}/categories", new { name = "X", slug = "mine" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(manager, HttpMethod.Post, $"{Root}/categories", new { name = " " })).StatusCode);

        var created = await Send(manager, HttpMethod.Post, $"{Root}/categories", new { name = "Smart Watches", description = "Wearables" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("no-store", created.Headers.CacheControl?.ToString());
        var category = (await created.Content.ReadFromJsonAsync<ManagedCategory>())!;
        Assert.Equal("smart-watches", category.Slug);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(manager, HttpMethod.Post, $"{Root}/categories", new { name = "Smart Watches" })).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await Send(manager, HttpMethod.Post, $"{Root}/products", NewProduct(category.Id, "WATCH-1", price: -5))).StatusCode);
        var product = await Send(manager, HttpMethod.Post, $"{Root}/products", NewProduct(category.Id, "WATCH-1"));
        Assert.Equal(HttpStatusCode.Created, product.StatusCode);
        var item = (await product.Content.ReadFromJsonAsync<ManagedProduct>())!;
        Assert.Equal(1500.50m, item.Price); Assert.Equal(7, item.StockQuantity); Assert.Equal("HTTP Distribution", item.DistributorName);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(manager, HttpMethod.Post, $"{Root}/products", NewProduct(category.Id, "WATCH-1"))).StatusCode);

        // Shoppers see the new product, then do not see it after it is removed.
        var publicDetail = $"/api/products/{item.Id}";
        Assert.Equal(HttpStatusCode.OK, (await guest.GetAsync(publicDetail)).StatusCode);
        var edit = await Send(manager, HttpMethod.Put, $"{Root}/products/{item.Id}", new
        {
            categoryId = category.Id, sku = "WATCH-1", name = "HTTP watch", model = "H-1", serialNumber = "SN-WATCH-1",
            description = "Edited over HTTP.", price = 1750m, warrantyStatus = "3 years", distributorName = "HTTP Distribution",
            distributorContact = "http@example.com", isActive = true
        });
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        Assert.Equal("HTTP watch", (await edit.Content.ReadFromJsonAsync<ManagedProduct>())!.Name);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(manager, HttpMethod.Delete, $"{Root}/categories/{category.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Send(manager, HttpMethod.Delete, $"{Root}/products/{item.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync(publicDetail)).StatusCode);
        Assert.False((await manager.GetFromJsonAsync<ManagedProductPage>($"{Root}/products"))!.Products.Single(p => p.Id == item.Id).IsActive);

        // A category that still has a (removed) product cannot be deleted, an empty one can.
        Assert.Equal(HttpStatusCode.Conflict, (await Send(manager, HttpMethod.Delete, $"{Root}/categories/{category.Id}")).StatusCode);
        var empty = (await (await Send(manager, HttpMethod.Post, $"{Root}/categories", new { name = "Empty one" })).Content.ReadFromJsonAsync<ManagedCategory>())!;
        Assert.Equal(HttpStatusCode.NoContent, (await Send(manager, HttpMethod.Delete, $"{Root}/categories/{empty.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Send(manager, HttpMethod.Delete, $"{Root}/categories/{empty.Id}")).StatusCode);
    }

    [MySqlFact]
    public async Task MySql_TheSameSkuOrCategoryNameSavedTwiceAtOnceHasOneWinner()
    {
        await fixture.ResetAsync(); User manager; long categoryId;
        await using (var seed = fixture.Create())
        {
            manager = new User { Name = "Manager", Email = "pm-race@example.com", PasswordHash = "hash", TaxId = "-", HomeAddress = "-", Role = UserRole.ProductManager };
            seed.Users.Add(manager); await seed.SaveChangesAsync();
            categoryId = (await new CatalogManagementService(seed, TimeProvider.System).CreateCategoryAsync(manager.Id, new("Race", null, null), default)).Id;
        }
        async Task<int> Run(Func<CatalogManagementService, Task> action)
        {
            await using var db = fixture.Create();
            try { await action(new CatalogManagementService(db, TimeProvider.System)); return 201; }
            catch (PurchaseException error) { return error.Status; }
        }
        var product = new ProductCreate(categoryId, null, "RACE-1", "Race phone", "R-1", "SN-R", "Race description", 10m, 3, "1 year", "Race Dist", "race@example.com");
        var skuResults = await Task.WhenAll(Run(s => s.CreateProductAsync(manager.Id, product, default)),
            Run(s => s.CreateProductAsync(manager.Id, product, default)));
        Assert.Equal([201, 409], skuResults.Order().ToArray());
        var nameResults = await Task.WhenAll(Run(s => s.CreateCategoryAsync(manager.Id, new("Twin", null, null), default)),
            Run(s => s.CreateCategoryAsync(manager.Id, new("Twin", null, null), default)));
        Assert.Equal([201, 409], nameResults.Order().ToArray());
        await using var check = fixture.Create();
        Assert.Single(await check.Products.Where(p => p.Sku == "RACE-1").ToListAsync());
        Assert.Single(await check.Categories.Where(c => c.Name == "Twin").ToListAsync());
        Assert.Single(await check.Distributors.Where(d => d.Name == "Race Dist").ToListAsync());
    }

    [MySqlFact]
    public async Task MySql_StockHttpIsProductManagerOnlyNeverBelowZeroAndShownAtOnceToShoppers()
    {
        await fixture.ResetAsync(); ReviewFixture.Graph g;
        await using (var seed = fixture.Create()) g = await ReviewFixture.SeedAsync(seed);
        using var app = new ManagementApplication(fixture);
        using var guest = app.CreateClient(); using var customer = app.CreateClient(); using var manager = app.CreateClient();
        var path = $"{Root}/products/{g.Product.Id}/stock";
        long productId = g.Product.Id;
        int current;
        await using (var read = fixture.Create()) current = (await read.Products.SingleAsync(p => p.Id == productId)).StockQuantity;

        // Guests and customers cannot change the stock.
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(guest, HttpMethod.Put, path, new { quantity = 5, expectedQuantity = current })).StatusCode);
        await Login(customer, g.Buyer.Email);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(customer, HttpMethod.Put, path, new { quantity = 5, expectedQuantity = current })).StatusCode);

        await SetRole(g.Other.Id, UserRole.ProductManager);
        await Login(manager, g.Other.Email);
        // No CSRF token, a negative count, an unknown field, a missing expected count: all refused, the stock stays.
        Assert.Equal(HttpStatusCode.BadRequest, (await manager.PutAsJsonAsync(path, new { quantity = 5, expectedQuantity = current })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(manager, HttpMethod.Put, path, new { quantity = -1, expectedQuantity = current })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(manager, HttpMethod.Put, path, new { quantity = 5 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(manager, HttpMethod.Put, path, new { quantity = 5, expectedQuantity = current, price = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Send(manager, HttpMethod.Put, $"{Root}/products/9999999/stock", new { quantity = 5, expectedQuantity = 0 })).StatusCode);
        Assert.Equal(current, (await guest.GetFromJsonAsync<JsonElement>($"/api/products/{productId}")).GetProperty("stockQuantity").GetInt32());

        // A stale screen is refused, the right count is accepted, and shoppers see the new count at once.
        Assert.Equal(HttpStatusCode.Conflict, (await Send(manager, HttpMethod.Put, path, new { quantity = 5, expectedQuantity = current + 1 })).StatusCode);
        var ok = await Send(manager, HttpMethod.Put, path, new { quantity = 7, expectedQuantity = current });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal(7, (await ok.Content.ReadFromJsonAsync<ManagedProduct>())!.StockQuantity);
        var detail = await guest.GetFromJsonAsync<JsonElement>($"/api/products/{productId}");
        Assert.Equal(7, detail.GetProperty("stockQuantity").GetInt32()); Assert.True(detail.GetProperty("inStock").GetBoolean());

        // At zero the product shows as out of stock and a customer cannot put it in the cart.
        Assert.Equal(HttpStatusCode.OK, (await Send(manager, HttpMethod.Put, path, new { quantity = 0, expectedQuantity = 7 })).StatusCode);
        Assert.False((await guest.GetFromJsonAsync<JsonElement>($"/api/products/{productId}")).GetProperty("inStock").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, (await Send(customer, HttpMethod.Post, "/api/cart/items", new { productId, quantity = 1 })).StatusCode);
    }

    [MySqlFact]
    public async Task MySql_TwoStockChangesAtOnceHaveOneWinnerAndASaleIsNeverOverwritten()
    {
        await fixture.ResetAsync(); User manager; long productId;
        await using (var seed = fixture.Create())
        {
            manager = new User { Name = "Manager", Email = "pm-stock@example.com", PasswordHash = "hash", TaxId = "-", HomeAddress = "-", Role = UserRole.ProductManager };
            seed.Users.Add(manager); await seed.SaveChangesAsync();
            var service = new CatalogManagementService(seed, TimeProvider.System);
            var category = await service.CreateCategoryAsync(manager.Id, new("Stock race", null, null), default);
            productId = (await service.CreateProductAsync(manager.Id, new ProductCreate(category.Id, null, "STOCK-1", "Stock phone", "S-1", "SN-S", "Stock description", 10m, 10, "1 year", "Stock Dist", "s@example.com"), default)).Id;
        }
        async Task<int> Set(int quantity, int expected)
        {
            await using var db = fixture.Create();
            try { await new CatalogManagementService(db, TimeProvider.System).SetStockAsync(manager.Id, productId, new(quantity, expected), default); return 200; }
            catch (PurchaseException error) { return error.Status; }
        }
        // Two managers saw 10 and save different counts at once: one wins, the other is told to look again.
        Assert.Equal([200, 409], (await Task.WhenAll(Set(20, 10), Set(30, 10))).Order().ToArray());
        int winner;
        await using (var read = fixture.Create()) winner = (await read.Products.SingleAsync(p => p.Id == productId)).StockQuantity;
        Assert.Contains(winner, new[] { 20, 30 });

        // A checkout takes 3 units (the same guarded UPDATE as CheckoutService) while a manager still sees the old count.
        await using (var sale = fixture.Create())
            Assert.Equal(1, await sale.Products.Where(p => p.Id == productId && p.StockQuantity >= 3)
                .ExecuteUpdateAsync(u => u.SetProperty(p => p.StockQuantity, p => p.StockQuantity - 3)));
        Assert.Equal(409, await Set(100, winner));
        await using var check = fixture.Create();
        Assert.Equal(winner - 3, (await check.Products.SingleAsync(p => p.Id == productId)).StockQuantity);
    }

    private async Task SetRole(long userId, UserRole role)
    {
        await using var db = fixture.Create();
        var user = await db.Users.SingleAsync(u => u.Id == userId); user.Role = role;
        user.SecurityStamp = Guid.NewGuid().ToString("N"); await db.SaveChangesAsync();
    }

    private static async Task Login(HttpClient client, string email) =>
        Assert.Equal(HttpStatusCode.OK, (await Send(client, HttpMethod.Post, "/api/auth/login", new { email, password = ReviewFixture.Password })).StatusCode);

    private static async Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string path, object? body = null)
    {
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        using var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = JsonContent.Create(body);
        request.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        return await client.SendAsync(request);
    }

    private sealed class ManagementApplication(MySqlFixture fixture) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("MySql:ServerVersion", "8.4.13");
            builder.UseSetting("ConnectionStrings:DefaultConnection", "Server=localhost;Database=unused;User ID=unused;Password=unused");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<StoreDbContext>(); services.AddScoped(_ => fixture.Create());
            });
        }
    }
}
