using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OnlineStore.Accounts;
using OnlineStore.Data;
using OnlineStore.Models;
using OnlineStore.Shopping;

namespace OnlineStore.Tests;

public class WishlistEndpointsTests
{
    [Fact]
    public async Task Wishlist_RequiresCustomerRoleAndReturnsOnlyTheSignedInOwnersProducts()
    {
        await using var server = await TestServer.CreateAsync();

        using var anonymous = await server.Client.GetAsync("/api/customer/wishlist");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        using var forbidden = await server.GetAsync("/api/customer/wishlist", RolePolicies.ProductManager, 101);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        using var response = await server.GetAsync("/api/customer/wishlist", RolePolicies.Customer, 101);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var rows = await response.Content.ReadFromJsonAsync<List<WishlistProductDto>>();
        Assert.NotNull(rows);
        var item = Assert.Single(rows);
        Assert.Equal(1, item.ProductId);
        Assert.Equal("Owned phone", item.Product.Name);

        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("sku", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("serialNumber", json, StringComparison.OrdinalIgnoreCase);

        var (token, cookie) = await server.GetCsrfAsync(RolePolicies.Customer, 102);
        using var deleteResponse = await server.DeleteAsync(
            "/api/customer/wishlist/1", RolePolicies.Customer, 102, token, cookie);
        Assert.True(deleteResponse.StatusCode == HttpStatusCode.NoContent,
            await deleteResponse.Content.ReadAsStringAsync());
        using var ownerResponse = await server.GetAsync("/api/customer/wishlist", RolePolicies.Customer, 101);
        var ownerItems = await ownerResponse.Content.ReadFromJsonAsync<List<WishlistProductDto>>();
        Assert.Single(ownerItems!);
    }

    private sealed class TestServer : IAsyncDisposable
    {
        private TestServer(WebApplication app, HttpClient client)
        {
            App = app;
            Client = client;
        }

        private WebApplication App { get; }
        public HttpClient Client { get; }

        public static async Task<TestServer> CreateAsync()
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            var databaseName = Guid.NewGuid().ToString();
            builder.Services.AddDbContext<StoreDbContext>(options => options.UseInMemoryDatabase(databaseName));
            builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");
            builder.Services.AddAuthentication("wishlist-test")
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("wishlist-test", _ => { });
            builder.Services.AddAuthorization(RolePolicies.Configure);

            var app = builder.Build();
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UsePrivateResponseNoStore();
            app.MapGet("/test-csrf", (HttpContext context, IAntiforgery antiforgery) =>
                Results.Ok(new { token = antiforgery.GetAndStoreTokens(context).RequestToken })).AllowAnonymous();
            app.MapWishlistEndpoints();

            var firstUser = new User { Id = 101, Name = "First", Email = "first@example.com", PasswordHash = "hash" };
            var secondUser = new User { Id = 102, Name = "Second", Email = "second@example.com", PasswordHash = "hash" };
            var category = new Category { Id = 1, Name = "Phones" };
            var product = new Product { Id = 1, Category = category, Sku = "PRIVATE-SKU", Name = "Owned phone", Price = 100, StockQuantity = 1 };
            var otherProduct = new Product { Id = 2, Category = category, Sku = "OTHER-SKU", Name = "Other customer's phone", Price = 200, StockQuantity = 1 };
            CatalogFixture.CompleteDetails(product);
            CatalogFixture.CompleteDetails(otherProduct);

            await using (var scope = app.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<StoreDbContext>();
                db.Users.AddRange(firstUser, secondUser);
                db.Products.AddRange(product, otherProduct);
                await db.SaveChangesAsync();
                db.WishlistItems.AddRange(
                    new WishlistItem { UserId = firstUser.Id, ProductId = product.Id },
                    new WishlistItem { UserId = secondUser.Id, ProductId = otherProduct.Id });
                await db.SaveChangesAsync();
            }

            await app.StartAsync();
            return new TestServer(app, app.GetTestClient());
        }

        public Task<HttpResponseMessage> GetAsync(string path, string role, long userId)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Add("X-Test-Role", role);
            request.Headers.Add("X-Test-User-Id", userId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return Client.SendAsync(request);
        }

        public async Task<(string Token, string Cookie)> GetCsrfAsync(string role, long userId)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/test-csrf");
            request.Headers.Add("X-Test-Role", role);
            request.Headers.Add("X-Test-User-Id", userId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            using var response = await Client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<CsrfResponse>();
            var cookie = response.Headers.GetValues("Set-Cookie").Single().Split(';', 2)[0];
            return (result!.Token!, cookie);
        }

        public Task<HttpResponseMessage> DeleteAsync(string path, string role, long userId, string token, string cookie)
        {
            var request = new HttpRequestMessage(HttpMethod.Delete, path);
            request.Headers.Add("X-Test-Role", role);
            request.Headers.Add("X-Test-User-Id", userId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            request.Headers.Add("X-CSRF-TOKEN", token);
            request.Headers.Add("Cookie", cookie);
            return Client.SendAsync(request);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await App.DisposeAsync();
        }
    }

    private sealed record CsrfResponse(string? Token);

    private sealed class TestAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Test-Role", out var role))
                return Task.FromResult(AuthenticateResult.NoResult());
            var claims = new[]
            {
                new Claim(ClaimTypes.Role, role.ToString()),
                new Claim(ClaimTypes.NameIdentifier, Request.Headers["X-Test-User-Id"].ToString())
            };
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "wishlist-test"));
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(principal, "wishlist-test")));
        }
    }
}
