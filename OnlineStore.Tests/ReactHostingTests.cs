using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OnlineStore.Data;

namespace OnlineStore.Tests;

public class ReactHostingTests
{
    [Theory]
    [InlineData("/")]
    [InlineData("/products")]
    [InlineData("/checkout")]
    [InlineData("/order-confirmation?orderId=17")]
    [InlineData("/deliveries")]
    public async Task NavigationAndRefresh_ServeTheReactEntryWithoutLogin(string path)
    {
        using var app = new ReactApplication();
        using var client = app.CreateClient();
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType!.MediaType);
        Assert.Contains("SPA-MARKER", await response.Content.ReadAsStringAsync());
        Assert.Contains("no-cache", response.Headers.CacheControl!.ToString());
    }

    [Theory]
    [InlineData("/api")]
    [InlineData("/api/not-a-route")]
    [InlineData("/API/not-a-route")]
    [InlineData("/api/missing.json")]
    [InlineData("/assets/missing.js")]
    [InlineData("/assets/no-extension")]
    [InlineData("/missing.css")]
    public async Task MissingApiAndAssets_Are404AndNeverHtml(string path)
    {
        using var app = new ReactApplication();
        using var client = app.CreateClient();
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("SPA-MARKER", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AssetsAndApi_KeepTheirContentTypesAndAuthorization()
    {
        using var app = new ReactApplication();
        using var client = app.CreateClient();
        var asset = await client.GetAsync("/assets/app.js");
        Assert.Equal(HttpStatusCode.OK, asset.StatusCode);
        Assert.Contains("javascript", asset.Content.Headers.ContentType!.MediaType);
        Assert.Equal("console.log('asset');", await asset.Content.ReadAsStringAsync());
        var health = await client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.Equal("application/json", health.Content.Headers.ContentType!.MediaType);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/customer/checkout")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/product-management/deliveries")).StatusCode);
        var token = await client.GetAsync("/api/auth/csrf");
        Assert.Equal(HttpStatusCode.OK, token.StatusCode);
        Assert.Equal("application/json", token.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task MissingReactBuild_DoesNotBreakTheApiOrSilentlyServeTheLegacyHome()
    {
        using var app = new ReactApplication(missingBuild: true);
        using var client = app.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/health")).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/checkout")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/not-a-route")).StatusCode);
    }

    private sealed class ReactApplication : WebApplicationFactory<Program>
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "react-hosting-" + Guid.NewGuid().ToString("N"));
        public ReactApplication(bool missingBuild = false)
        {
            if (missingBuild) return;
            Directory.CreateDirectory(Path.Combine(root, "assets"));
            File.WriteAllText(Path.Combine(root, "index.html"), "<!doctype html><html><body>SPA-MARKER</body></html>");
            File.WriteAllText(Path.Combine(root, "assets", "app.js"), "console.log('asset');");
        }
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("Frontend:DistPath", root);
            builder.UseSetting("ConnectionStrings:DefaultConnection", "Server=localhost;Database=unused;User ID=unused;Password=unused");
            var name = Guid.NewGuid().ToString("N");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<StoreDbContext>();
                services.AddScoped(_ => new StoreDbContext(new DbContextOptionsBuilder<StoreDbContext>().UseInMemoryDatabase(name).Options));
            });
        }
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
