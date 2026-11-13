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
using OnlineStore.Models;
using OnlineStore.Purchasing;

namespace OnlineStore.Tests;

// SCRUM-103: PUT /api/customer/profile.
public class CustomerProfileUpdateTests
{
    private const string Password = "A long test passphrase!";
    private const string Email = "customer@example.com";
    private static RegisterRequest Registration => new("Customer", Email, "TAX-123", "10 Example Street", Password);
    private static object Update => new { name = "  New Name  ", taxId = " 10000000146 ", homeAddress = " 22 New Street\nIstanbul " };

    [Fact]
    public async Task Owner_CanUpdateNameTaxIdAndAddress_AndTheValuesAreTrimmed()
    {
        using var app = new ProfileApplication();
        using var client = await SignedInCustomerAsync(app);
        var response = await SendAsync(client, HttpMethod.Put, "/api/customer/profile", Update);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
        var profile = (await response.Content.ReadFromJsonAsync<CustomerProfileDto>())!;
        Assert.Equal("New Name", profile.Name);
        Assert.Equal("10000000146", profile.TaxId);
        Assert.Equal("22 New Street\nIstanbul", profile.HomeAddress);

        var reread = await client.GetFromJsonAsync<CustomerProfileDto>("/api/customer/profile");
        Assert.Equal(profile, reread);
        using var scope = app.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<StoreDbContext>().Users.Include(u => u.Addresses).SingleAsync();
        Assert.Equal("New Name", user.Name);
        Assert.Single(user.Addresses, address => address.IsDefault && address.IsActive);
    }

    [Fact]
    public async Task Email_IsUnchangedAndCannotBeSent()
    {
        using var app = new ProfileApplication();
        using var client = await SignedInCustomerAsync(app);
        var withEmail = await SendAsync(client, HttpMethod.Put, "/api/customer/profile",
            new { name = "New Name", taxId = "TAX-9", homeAddress = "Elsewhere", email = "attacker@example.com" });
        Assert.Equal(HttpStatusCode.BadRequest, withEmail.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Put, "/api/customer/profile", Update)).StatusCode);
        var profile = await client.GetFromJsonAsync<CustomerProfileDto>("/api/customer/profile");
        Assert.Equal(Email, profile!.Email);
        // The email blind index still matches, so the account can log in with it again.
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Post, "/api/auth/logout", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Post, "/api/auth/login", new LoginRequest(Email, Password))).StatusCode);
    }

    [Fact]
    public async Task SignedOut_Gets401()
    {
        using var app = new ProfileApplication();
        using var client = app.CreateClient();
        var response = await SendAsync(client, HttpMethod.Put, "/api/customer/profile", Update);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(UserRole.SalesManager)]
    [InlineData(UserRole.ProductManager)]
    [InlineData(UserRole.SupportAgent)]
    public async Task StaffRoles_AreRejectedLikeGet(UserRole role)
    {
        using var app = new ProfileApplication();
        using var client = app.CreateClient();
        await SendAsync(client, HttpMethod.Post, "/api/auth/register", Registration);
        using (var scope = app.Services.CreateScope())
            Assert.True(await scope.ServiceProvider.GetRequiredService<AccountService>().SetRoleAsync(Email, role));
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Post, "/api/auth/login", new LoginRequest(Email, Password))).StatusCode);
        var get = await client.GetAsync("/api/customer/profile");
        var put = await SendAsync(client, HttpMethod.Put, "/api/customer/profile", Update);
        Assert.Equal(HttpStatusCode.Forbidden, get.StatusCode);
        Assert.Equal(get.StatusCode, put.StatusCode);
        using var check = app.Services.CreateScope();
        Assert.Equal("Customer", (await check.ServiceProvider.GetRequiredService<StoreDbContext>().Users.SingleAsync()).Name);
    }

    [Fact]
    public async Task SuppliedCustomerIds_AreIgnoredOrRejected()
    {
        using var app = new ProfileApplication();
        using var client = app.CreateClient();
        await SendAsync(client, HttpMethod.Post, "/api/auth/register", Registration with { Email = "other@example.com" });
        await SendAsync(client, HttpMethod.Post, "/api/auth/register", Registration);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Post, "/api/auth/login", new LoginRequest(Email, Password))).StatusCode);
        long otherId;
        using (var scope = app.Services.CreateScope())
            otherId = (await scope.ServiceProvider.GetRequiredService<StoreDbContext>().Users.ToListAsync()).Single(u => u.Email == "other@example.com").Id;

        // An ID in the URL is not read: the session's own profile changes.
        var viaQuery = await SendAsync(client, HttpMethod.Put, $"/api/customer/profile?userId={otherId}&id={otherId}", Update);
        Assert.Equal(HttpStatusCode.OK, viaQuery.StatusCode);
        Assert.Equal(Email, (await viaQuery.Content.ReadFromJsonAsync<CustomerProfileDto>())!.Email);
        // An ID in the body is an unknown property.
        var viaBody = await SendAsync(client, HttpMethod.Put, "/api/customer/profile",
            new { id = otherId, name = "Hijacked", taxId = "TAX-0", homeAddress = "Nowhere" });
        Assert.Equal(HttpStatusCode.BadRequest, viaBody.StatusCode);

        using var check = app.Services.CreateScope();
        var other = (await check.ServiceProvider.GetRequiredService<StoreDbContext>().Users.Include(u => u.Addresses).ToListAsync())
            .Single(u => u.Id == otherId);
        Assert.Equal("Customer", other.Name);
        Assert.Equal("TAX-123", other.TaxId);
        Assert.Equal("10 Example Street", other.HomeAddress);
    }

    [Fact]
    public async Task MissingCsrfToken_IsRejectedAndNothingChanges()
    {
        using var app = new ProfileApplication();
        using var client = await SignedInCustomerAsync(app);
        var response = await client.PutAsJsonAsync("/api/customer/profile", Update);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("anti-forgery", await response.Content.ReadAsStringAsync());
        var profile = await client.GetFromJsonAsync<CustomerProfileDto>("/api/customer/profile");
        Assert.Equal("Customer", profile!.Name);
    }

    [Theory]
    [InlineData("name", "")]
    [InlineData("taxId", "   ")]
    [InlineData("homeAddress", "")]
    [InlineData("name", null)]
    public async Task EmptyFields_Return400WithFieldErrors(string field, string? value)
    {
        using var app = new ProfileApplication();
        using var client = await SignedInCustomerAsync(app);
        var body = new Dictionary<string, string?> { ["name"] = "Name", ["taxId"] = "TAX-1", ["homeAddress"] = "Street" };
        body[field] = value;
        var response = await SendAsync(client, HttpMethod.Put, "/api/customer/profile", body);
        await AssertFieldErrorAsync(response, field, "This field is required.");
    }

    [Theory]
    [InlineData("name", 101, "Use at most 100 characters.")]
    [InlineData("taxId", 65, "Use at most 64 characters.")]
    [InlineData("homeAddress", 1001, "Use at most 1000 characters.")]
    public async Task TooLongFields_Return400WithFieldErrors(string field, int length, string message)
    {
        using var app = new ProfileApplication();
        using var client = await SignedInCustomerAsync(app);
        var body = new Dictionary<string, string?> { ["name"] = "Name", ["taxId"] = "TAX-1", ["homeAddress"] = "Street" };
        body[field] = new string('a', length);
        var response = await SendAsync(client, HttpMethod.Put, "/api/customer/profile", body);
        await AssertFieldErrorAsync(response, field, message);
        var profile = await client.GetFromJsonAsync<CustomerProfileDto>("/api/customer/profile");
        Assert.Equal("Customer", profile!.Name);
        Assert.Equal("TAX-123", profile.TaxId);
    }

    [Fact]
    public async Task Response_HasNoHashOrSecurityStamp()
    {
        using var app = new ProfileApplication();
        using var client = await SignedInCustomerAsync(app);
        var json = await (await SendAsync(client, HttpMethod.Put, "/api/customer/profile", Update)).Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        Assert.Equal(["id", "name", "email", "taxId", "homeAddress"],
            document.RootElement.EnumerateObject().Select(property => property.Name).ToArray());
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stamp", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LegacyBlankProfile_BlocksCheckoutUntilCompleted()
    {
        using var app = new ProfileApplication();
        using var client = await SignedInCustomerAsync(app);
        long userId;
        using (var scope = app.Services.CreateScope())
        {
            // A legacy account from before SCRUM-71: no tax ID and no default address, but an open cart.
            var db = scope.ServiceProvider.GetRequiredService<StoreDbContext>();
            var user = await db.Users.Include(u => u.Addresses).SingleAsync();
            user.TaxId = "";
            db.Addresses.RemoveRange(user.Addresses);
            var product = new Product { Sku = "LEGACY", Name = "Legacy product", Price = 50, StockQuantity = 3,
                Category = new Category { Name = "Legacy category" } };
            CatalogFixture.CompleteDetails(product);
            db.Products.Add(product);
            await db.SaveChangesAsync();
            db.Carts.Add(new Cart { UserId = user.Id, Items = [new CartItem { ProductId = product.Id, Quantity = 1 }] });
            await db.SaveChangesAsync();
            userId = user.Id;
        }
        var blank = await client.GetFromJsonAsync<CustomerProfileDto>("/api/customer/profile");
        Assert.Equal("", blank!.TaxId);
        Assert.Equal("", blank.HomeAddress);

        using (var scope = app.Services.CreateScope())
        {
            var refused = await Assert.ThrowsAsync<PurchaseException>(() => Checkout(scope).QuoteAsync(userId, default));
            Assert.Equal(409, refused.Status);
            Assert.Equal("Complete your customer profile before checkout.", refused.Message);
        }

        var saved = await SendAsync(client, HttpMethod.Put, "/api/customer/profile",
            new { name = "Customer", taxId = "10000000146", homeAddress = "5 Completed Street" });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        using (var scope = app.Services.CreateScope())
        {
            var quote = await Checkout(scope).QuoteAsync(userId, default);
            Assert.Equal("10000000146", quote.TaxId);
            Assert.Equal("5 Completed Street", quote.Address);
            Assert.Equal(50m, quote.Total);
        }
    }

    private static CheckoutService Checkout(IServiceScope scope) => new(scope.ServiceProvider.GetRequiredService<StoreDbContext>(),
        new MockPaymentGateway(TimeProvider.System), TimeProvider.System);

    private static async Task AssertFieldErrorAsync(HttpResponseMessage response, string field, string message)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = document.RootElement.GetProperty("errors");
        Assert.Equal(message, errors.GetProperty(field)[0].GetString());
        // The stored email is checked too, but it is valid, so it never shows up as an error.
        Assert.False(errors.TryGetProperty("email", out _));
    }

    private static async Task<HttpClient> SignedInCustomerAsync(ProfileApplication app)
    {
        var client = app.CreateClient();
        Assert.Equal(HttpStatusCode.Created, (await SendAsync(client, HttpMethod.Post, "/api/auth/register", Registration)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Post, "/api/auth/login", new LoginRequest(Email, Password))).StatusCode);
        return client;
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, object body)
    {
        var tokens = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", tokens.GetProperty("token").GetString());
        return await client.SendAsync(request);
    }

    private sealed class ProfileApplication : WebApplicationFactory<Program>
    {
        private readonly string databaseName = Guid.NewGuid().ToString();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:DefaultConnection",
                "Server=127.0.0.1;Port=1;Database=account_test;User ID=test;Password=test;Connection Timeout=1;Pooling=false");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<StoreDbContext>();
                services.AddScoped(_ => new StoreDbContext(new DbContextOptionsBuilder<StoreDbContext>()
                    .UseInMemoryDatabase(databaseName).Options));
            });
        }
    }
}
