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
using OnlineStore.Invoicing;
using OnlineStore.Models;
using OnlineStore.Purchasing;

namespace OnlineStore.Tests;

// SCRUM-74 against real MySQL: the checkout issues the invoice and only the right people can download it.
[Collection("MySql")]
public class InvoiceMySqlTests(MySqlFixture fixture) : IClassFixture<MySqlFixture>, IDisposable
{
    private const string Password = "Invoice test passphrase!";
    private readonly string storageRoot = Path.Combine(Path.GetTempPath(), $"invoices-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(storageRoot)) Directory.Delete(storageRoot, recursive: true);
    }

    private async Task<(User Buyer, User Other, User Product, User Sales)> SeedAsync()
    {
        await fixture.ResetAsync();
        await using var db = fixture.Create();
        User NewUser(string email, UserRole role = UserRole.Customer)
        {
            var user = new User { Name = "Invoice buyer", Email = email, TaxId = "TAX-ORIGINAL",
                HomeAddress = "Original address", PasswordHash = "", Role = role };
            user.PasswordHash = new Microsoft.AspNetCore.Identity.PasswordHasher<User>().HashPassword(user, Password);
            return user;
        }
        var users = (Buyer: NewUser("invoice@example.com"), Other: NewUser("other-invoice@example.com"),
            Product: NewUser("pm-invoice@example.com", UserRole.ProductManager),
            Sales: NewUser("sm-invoice@example.com", UserRole.SalesManager));
        var product = new Product { Sku = "INVOICE", Name = "Invoice phone", Price = 100, StockQuantity = 5,
            Category = new Category { Name = "Invoice category" } };
        CatalogFixture.CompleteDetails(product);
        db.Users.AddRange(users.Buyer, users.Other, users.Product, users.Sales);
        db.Products.Add(product);
        await db.SaveChangesAsync();
        db.Carts.Add(new Cart { UserId = users.Buyer.Id, Items = [new CartItem { ProductId = product.Id, Quantity = 2 }] });
        await db.SaveChangesAsync();
        return users;
    }

    [MySqlFact]
    public async Task MySql_Checkout_IssuesInvoiceThatOnlyOwnerAndManagersCanDownload()
    {
        var users = await SeedAsync();
        using var app = new InvoiceApplication(fixture, storageRoot);
        using var anonymous = app.CreateClient();
        using var buyer = app.CreateClient();
        using var other = app.CreateClient();
        using var productManager = app.CreateClient();
        using var salesManager = app.CreateClient();
        await Login(buyer, users.Buyer.Email); await Login(other, users.Other.Email);
        await Login(productManager, users.Product.Email); await Login(salesManager, users.Sales.Email);

        var quote = (await buyer.GetFromJsonAsync<CheckoutQuote>("/api/customer/checkout"))!;
        var card = new MockCard { Number = "4242424242424242", ExpiryMonth = 12, ExpiryYear = DateTime.UtcNow.Year + 1, SecurityCode = "123" };
        var request = new CheckoutRequest(quote.CartId, Guid.NewGuid().ToString("D"), quote.QuoteToken, card);
        var purchased = await Post(buyer, "/api/customer/checkout", request);
        Assert.Equal(HttpStatusCode.Created, purchased.StatusCode);
        var receipt = (await purchased.Content.ReadFromJsonAsync<PurchaseReceipt>())!;
        Assert.Equal("Ready", receipt.InvoiceStatus);
        Assert.Equal($"/api/customer/orders/{receipt.OrderId}/invoice", receipt.InvoiceUrl);

        // A retry of the same checkout returns the same order and does not issue a second invoice.
        var replay = (await (await Post(buyer, "/api/customer/checkout", request)).Content.ReadFromJsonAsync<PurchaseReceipt>())!;
        Assert.Equal(receipt.OrderId, replay.OrderId);
        Assert.Equal("Ready", replay.InvoiceStatus);

        var download = await buyer.GetAsync(receipt.InvoiceUrl);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("application/pdf", download.Content.Headers.ContentType!.MediaType);
        Assert.Equal("no-store", download.Headers.CacheControl!.ToString());
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(await download.Content.ReadAsByteArrayAsync(), 0, 4));

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(receipt.InvoiceUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(receipt.InvoiceUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await productManager.GetAsync(receipt.InvoiceUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await productManager.GetAsync($"/api/product-management/invoices/{receipt.OrderId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await salesManager.GetAsync($"/api/sales/invoices/{receipt.OrderId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await buyer.GetAsync($"/api/sales/invoices/{receipt.OrderId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await salesManager.GetAsync($"/api/product-management/invoices/{receipt.OrderId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await salesManager.GetAsync($"/api/sales/invoices/{receipt.OrderId + 1000}")).StatusCode);

        // The invoice keeps the purchase-time snapshot after the customer edits the profile.
        await using (var changes = fixture.Create())
        {
            var buyerRow = await changes.Users.FindAsync(users.Buyer.Id);
            buyerRow!.HomeAddress = "New address";
            await changes.SaveChangesAsync();
        }
        await using var check = fixture.Create();
        var invoice = await check.Invoices.SingleAsync();
        Assert.Equal("Original address", invoice.AddressSnapshot);
        Assert.Equal($"INV-{DateTime.UtcNow.Year}-{receipt.OrderId:D6}", invoice.InvoiceNumber);
        Assert.True(File.Exists(Path.Combine(storageRoot, invoice.ProtectedDocumentKey!)));
    }

    [MySqlFact]
    public async Task MySql_ConcurrentRequests_IssueExactlyOneInvoice()
    {
        var users = await SeedAsync();
        int orderId;
        await using (var setup = fixture.Create())
        {
            var product = await setup.Products.SingleAsync();
            var order = new Order { UserId = users.Buyer.Id, CustomerNameSnapshot = "Buyer", TaxIdSnapshot = "TAX",
                EmailSnapshot = "invoice@example.com", AddressSnapshot = "Address" };
            order.OrderItems.Add(OrderItem.Snapshot(product, 1));
            order.ConfirmPayment(DateTime.UtcNow);
            setup.Orders.Add(order);
            await setup.SaveChangesAsync();
            orderId = order.Id;
        }

        var storage = new FileInvoiceStorage(storageRoot);
        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(async _ =>
        {
            await using var db = fixture.Create();
            return await new InvoiceService(db, storage, TimeProvider.System).EnsureAsync(orderId, default);
        }));

        Assert.All(results, Assert.True);
        await using var check = fixture.Create();
        var invoice = await check.Invoices.SingleAsync();
        Assert.NotNull(invoice.ProtectedDocumentKey);
    }

    private static async Task Login(HttpClient client, string email) =>
        Assert.Equal(HttpStatusCode.OK, (await Post(client, "/api/auth/login", new LoginRequest(email, Password))).StatusCode);

    private static async Task<HttpResponseMessage> Post(HttpClient client, string path, object body)
    {
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        return await client.SendAsync(request);
    }

    private sealed class InvoiceApplication(MySqlFixture fixture, string storageRoot) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:DefaultConnection", "Server=localhost;Database=unused;User ID=unused;Password=unused");
            builder.UseSetting("Invoices:StoragePath", storageRoot);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<StoreDbContext>();
                services.AddScoped(_ => fixture.Create());
            });
        }
    }
}
