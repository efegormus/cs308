using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OnlineStore.Accounts;
using OnlineStore.Data;
using OnlineStore.Models;
using OnlineStore.Purchasing;

namespace OnlineStore.Tests;

[Collection("MySql")]
public class PurchasingMySqlTests(MySqlFixture fixture) : IClassFixture<MySqlFixture>
{
    private const string Password = "Purchasing test passphrase!";
    private static MockCard Card(string number = "4242424242424242") => new()
        { Number = number, ExpiryMonth = 12, ExpiryYear = DateTime.UtcNow.Year + 1, SecurityCode = "123" };
    private static ICheckoutPaymentGateway Gateway() => new MockPaymentGateway(TimeProvider.System);
    private CheckoutService Service(StoreDbContext db, ICheckoutPaymentGateway? gateway = null) => new(db, gateway ?? Gateway(), TimeProvider.System);

    private async Task<(User Buyer, User Other, User Manager, Cart Cart, Product Product)> SeedAsync(int stock = 5)
    {
        await fixture.ResetAsync();
        await using var db = fixture.Create();
        User NewUser(string email, UserRole role = UserRole.Customer)
        {
            var user = new User { Name = "Buyer", Email = email, TaxId = "TAX-ORIGINAL", HomeAddress = "Original address", PasswordHash = "", Role = role };
            user.PasswordHash = new Microsoft.AspNetCore.Identity.PasswordHasher<User>().HashPassword(user, Password);
            return user;
        }
        var buyer = NewUser("checkout@example.com");
        var other = NewUser("other@example.com");
        var manager = NewUser("manager@example.com", UserRole.ProductManager);
        var product = new Product { Sku = "CHECKOUT", Name = "Original product", Price = 100, StockQuantity = stock,
            Category = new Category { Name = "Checkout category" } };
        CatalogFixture.CompleteDetails(product);
        db.Users.AddRange(buyer, other, manager);
        db.Products.Add(product);
        await db.SaveChangesAsync();
        var cart = new Cart { UserId = buyer.Id, Items = [new CartItem { ProductId = product.Id, Quantity = 1 }] };
        db.Carts.Add(cart);
        await db.SaveChangesAsync();
        return (buyer, other, manager, cart, product);
    }

    private static CheckoutRequest Request(CheckoutQuote quote, string? key = null, MockCard? card = null) =>
        new(quote.CartId, key ?? Guid.NewGuid().ToString("D"), quote.QuoteToken, card ?? Card());

    [MySqlFact]
    public async Task MySql_Checkout_PersistsDiscountedSnapshotsClearsCartAndReplaysOnce()
    {
        var g = await SeedAsync();
        await using (var setup = fixture.Create())
        {
            setup.Discounts.Add(new Discount { Rate = .2m, StartsAt = DateTime.UtcNow.AddDays(-1), EndsAt = DateTime.UtcNow.AddDays(1),
                Products = [new DiscountProduct { ProductId = g.Product.Id }] });
            await setup.SaveChangesAsync();
        }
        await using var first = fixture.Create();
        var quote = await Service(first).QuoteAsync(g.Buyer.Id, default);
        Assert.Equal(80m, quote.Total);
        var request = Request(quote);
        var purchased = await Service(first).CompleteAsync(g.Buyer.Id, request, default);
        Assert.False(purchased.Replayed);
        Assert.Equal(80m, purchased.Receipt.Total);
        Assert.Equal("Confirmed", purchased.Receipt.PaymentStatus);
        Assert.Equal("Processing", purchased.Receipt.Status);
        await using (var changes = fixture.Create())
        {
            var user = await changes.Users.FindAsync(g.Buyer.Id);
            user!.HomeAddress = "Changed address";
            var product = await changes.Products.FindAsync(g.Product.Id);
            product!.Price = 200; product.Name = "Changed name";
            await changes.SaveChangesAsync();
        }
        await using var retry = fixture.Create();
        var replay = await Service(retry).CompleteAsync(g.Buyer.Id, request, default);
        Assert.True(replay.Replayed);
        Assert.Equal(purchased.Receipt.OrderId, replay.Receipt.OrderId);
        Assert.Equal("Original address", replay.Receipt.Address);
        Assert.Equal("Original product", replay.Receipt.Items.Single().Name);
        Assert.Equal(80m, replay.Receipt.Total);
        Assert.Equal(4, (await retry.Products.SingleAsync()).StockQuantity);
        Assert.Single(await retry.Orders.ToListAsync());
        Assert.Single(await retry.Payments.ToListAsync());
        Assert.Empty(await retry.CartItems.ToListAsync());
        Assert.Equal(CartState.CheckedOut, (await retry.Carts.SingleAsync()).State);
        var line = await retry.OrderItems.SingleAsync();
        Assert.Equal(40m, line.UnitCost);
        Assert.NotNull(line.DiscountId);
        var delivery = await retry.Deliveries.Include(d => d.Items).SingleAsync();
        Assert.Equal("Original address", delivery.AddressSnapshot);
        Assert.Equal(80m, delivery.Items.Single().TotalPriceSnapshot);
        var paymentJson = JsonSerializer.Serialize(await retry.Payments.AsNoTracking().Select(p => new { p.ProviderReference, p.IdempotencyKey, p.Amount }).SingleAsync());
        Assert.DoesNotContain("4242424242424242", paymentJson);
    }

    [MySqlFact]
    public async Task MySql_Checkout_DeclineAndStaleQuoteLeaveStockAndCartUntouched()
    {
        var g = await SeedAsync();
        await using (var db = fixture.Create())
        {
            var quote = await Service(db).QuoteAsync(g.Buyer.Id, default);
            var declined = await Assert.ThrowsAsync<PurchaseException>(() => Service(db).CompleteAsync(g.Buyer.Id,
                Request(quote, card: Card("4000000000000002")), default));
            Assert.Equal(402, declined.Status);
        }
        await using (var db = fixture.Create())
        {
            var quote = await Service(db).QuoteAsync(g.Buyer.Id, default);
            await db.Products.ExecuteUpdateAsync(update => update.SetProperty(product => product.Price, 120m));
            Assert.Equal(409, (await Assert.ThrowsAsync<PurchaseException>(() =>
                Service(db).CompleteAsync(g.Buyer.Id, Request(quote), default))).Status);
        }
        await AssertUnchangedAsync(5);
    }

    [MySqlFact]
    public async Task MySql_Checkout_InvalidPaymentLeavesStockAndCartUntouched()
    {
        var g = await SeedAsync();
        await using (var db = fixture.Create())
        {
            var quote = await Service(db).QuoteAsync(g.Buyer.Id, default);
            var error = await Assert.ThrowsAsync<PurchaseException>(() => Service(db).CompleteAsync(g.Buyer.Id,
                Request(quote, card: Card("4242424242424241")), default));
            Assert.Equal(400, error.Status);
            Assert.Equal("Enter valid mock payment details.", error.Message);
        }
        await AssertUnchangedAsync(5);
    }
    [MySqlFact]
    public async Task MySql_Checkout_SaveFailureRollsBackStockOrderPaymentAndCart()
    {
        var g = await SeedAsync();
        await using var original = fixture.Create();
        var options = new DbContextOptionsBuilder<StoreDbContext>()
            .UseMySql(original.Database.GetConnectionString(), new MySqlServerVersion(new Version(8,4,13)))
            .AddInterceptors(new FailSave()).Options;
        await using (var db = new StoreDbContext(options))
        {
            var quote = await Service(db).QuoteAsync(g.Buyer.Id, default);
            await Assert.ThrowsAsync<InvalidOperationException>(() => Service(db).CompleteAsync(g.Buyer.Id, Request(quote), default));
        }
        await AssertUnchangedAsync(5);
    }

    [MySqlFact]
    public async Task MySql_Checkout_LastUnitHasOneWinnerAcrossCustomers()
    {
        var g = await SeedAsync(stock: 1);
        await using (var setup = fixture.Create())
        {
            setup.Carts.Add(new Cart { UserId = g.Other.Id, Items = [new CartItem { ProductId = g.Product.Id, Quantity = 1 }] });
            await setup.SaveChangesAsync();
        }
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var ready = 0;
        async Task<bool> Attempt(int userId)
        {
            await using var db = fixture.Create();
            var quote = await Service(db).QuoteAsync(userId, default);
            if (Interlocked.Increment(ref ready) == 2) gate.SetResult();
            await gate.Task;
            try { await Service(db).CompleteAsync(userId, Request(quote), default); return true; }
            catch (PurchaseException error) { Assert.Equal(409, error.Status); return false; }
        }
        var first = Attempt(g.Buyer.Id);
        var second = Attempt(g.Other.Id);
        var results = await Task.WhenAll(first, second);
        Assert.Single(results.Where(success => success));
        await using var check = fixture.Create();
        Assert.Equal(0, (await check.Products.SingleAsync()).StockQuantity);
        Assert.Single(await check.Payments.ToListAsync());
        Assert.Single(await check.Orders.ToListAsync());
        Assert.Single(await check.CartItems.ToListAsync());
    }

    [MySqlFact]
    public async Task MySql_Checkout_ConcurrentRetriesReturnOnePurchaseAndCannotBuyTheCartAgain()
    {
        var g = await SeedAsync();
        CheckoutQuote quote;
        await using (var db = fixture.Create()) quote = await Service(db).QuoteAsync(g.Buyer.Id, default);
        var request = Request(quote);
        async Task<CheckoutResult> Attempt()
        {
            await using var db = fixture.Create();
            return await Service(db).CompleteAsync(g.Buyer.Id, request, default);
        }
        var results = await Task.WhenAll(Attempt(), Attempt(), Attempt());
        Assert.Single(results.Where(result => !result.Replayed));
        Assert.Single(results.Select(result => result.Receipt.OrderId).Distinct());
        await using var check = fixture.Create();
        Assert.Equal(4, (await check.Products.SingleAsync()).StockQuantity);
        Assert.Equal(409, (await Assert.ThrowsAsync<PurchaseException>(() =>
            Service(check).CompleteAsync(g.Buyer.Id, Request(quote), default))).Status);
        Assert.Single(await check.Orders.ToListAsync());
    }

    [MySqlFact]
    public async Task MySql_Checkout_RejectsOtherOwnersAndKeyRebinding()
    {
        var g = await SeedAsync();
        await using var db = fixture.Create();
        var quote = await Service(db).QuoteAsync(g.Buyer.Id, default);
        var request = Request(quote);
        Assert.Equal(404, (await Assert.ThrowsAsync<PurchaseException>(() =>
            Service(db).CompleteAsync(g.Other.Id, request, default))).Status);
        // A failed service call rolls back its transaction; use a new request scope afterwards.
        await using var purchase = fixture.Create();
        await Service(purchase).CompleteAsync(g.Buyer.Id, request, default);
        await using var replay = fixture.Create();
        Assert.Equal(409, (await Assert.ThrowsAsync<PurchaseException>(() =>
            Service(replay).CompleteAsync(g.Buyer.Id, request with { CartId = g.Cart.Id + 1 }, default))).Status);
    }

    [MySqlFact]
    public async Task MySql_Delivery_EnforcesTransitionsVersionsSnapshotsAndUpdatesCustomerReceipt()
    {
        var g = await SeedAsync();
        await using (var db = fixture.Create())
        {
            var quote = await Service(db).QuoteAsync(g.Buyer.Id, default);
            await Service(db).CompleteAsync(g.Buyer.Id, Request(quote), default);
            await db.Users.Where(user => user.Id == g.Buyer.Id).ExecuteUpdateAsync(update => update.SetProperty(user => user.HomeAddress, "Later address"));
        }
        async Task<DeliveryView> Change(int id, string target, string version)
        {
            await using var db = fixture.Create();
            return await new DeliveryService(db, TimeProvider.System).TransitionAsync(id, new(target, version), default);
        }
        DeliveryView delivery;
        await using (var db = fixture.Create()) delivery = (await new DeliveryService(db, TimeProvider.System).ListAsync(0, default)).Items.Single();
        Assert.Equal(g.Buyer.Id, delivery.CustomerId);
        Assert.True(delivery.CanShip);
        Assert.False(delivery.CanDeliver);
        Assert.Equal("Original address", delivery.Address);
        Assert.Equal(409, (await Assert.ThrowsAsync<PurchaseException>(() => Change(delivery.Id, "Delivered", delivery.Version))).Status);
        var shipped = await Change(delivery.Id, "InTransit", delivery.Version);
        Assert.True(shipped.CanDeliver);
        Assert.Equal(409, (await Assert.ThrowsAsync<PurchaseException>(() => Change(delivery.Id, "Delivered", delivery.Version))).Status);
        var delivered = await Change(delivery.Id, "Delivered", shipped.Version);
        Assert.Equal("Delivered", delivered.Status);
        Assert.NotNull(delivered.DeliveredAt);
        Assert.False(delivered.CanShip);
        Assert.False(delivered.CanDeliver);
        Assert.Equal(409, (await Assert.ThrowsAsync<PurchaseException>(() => Change(delivery.Id, "InTransit", delivered.Version))).Status);
        await using var check = fixture.Create();
        var order = await check.Orders.Include(o => o.OrderItems).SingleAsync();
        Assert.Equal("Delivered", PurchaseViews.Receipt(order).Status);
        Assert.NotNull(order.DeliveredAt);
        // MySQL DATETIME(6) stores timestamps with microsecond precision.
        Assert.InRange(
            Math.Abs((delivered.DeliveredAt!.Value - order.DeliveredAt.Value).Ticks),
            0L,
            10L);
    }

    [MySqlFact]
    public async Task MySql_Delivery_RejectsUnpaidAndCancelledOrders()
    {
        var g = await SeedAsync();
        await using (var setup = fixture.Create())
        {
            var order = new Order { UserId = g.Buyer.Id, OrderItems = [OrderItem.Snapshot(g.Product, 1)] };
            order.OrderItems.Single().Product = null!; // Use the existing product FK, not the detached graph.
            order.Deliveries.Add(new Delivery { AddressSnapshot = "Original address", Items =
                [new DeliveryItem { OrderItem = order.OrderItems.Single(), Quantity = 1, TotalPriceSnapshot = 100 }] });
            order.AddressSnapshot = "Original address";
            setup.Orders.Add(order);
            await setup.SaveChangesAsync();
        }
        await using var db = fixture.Create();
        var service = new DeliveryService(db, TimeProvider.System);
        var delivery = (await service.ListAsync(0, default)).Items.Single();
        Assert.False(delivery.CanShip);
        Assert.Equal(409, (await Assert.ThrowsAsync<PurchaseException>(() => service.TransitionAsync(delivery.Id, new("InTransit", delivery.Version), default))).Status);
    }

    [MySqlFact]
    public async Task MySql_PurchaseHttp_UsesRealSessionsCsrfAndOwnerChecks()
    {
        var g = await SeedAsync();
        using var app = new PurchaseApplication(fixture);
        using var buyer = app.CreateClient();
        using var other = app.CreateClient();
        using var manager = app.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await buyer.GetAsync("/api/customer/checkout")).StatusCode);
        await Login(buyer, g.Buyer.Email); await Login(other, g.Other.Email); await Login(manager, g.Manager.Email);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.GetAsync("/api/customer/checkout")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await buyer.GetAsync("/api/product-management/deliveries")).StatusCode);
        var quote = (await buyer.GetFromJsonAsync<CheckoutQuote>("/api/customer/checkout"))!;
        var request = Request(quote);
        Assert.Equal(HttpStatusCode.BadRequest, (await buyer.PostAsJsonAsync("/api/customer/checkout", request)).StatusCode);
        var tampered = new { request.CartId, request.IdempotencyKey, request.QuoteToken, request.Payment, total = .01m, paid = true, userId = g.Other.Id };
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(buyer, "/api/customer/checkout", tampered)).StatusCode);
        var purchased = await Post(buyer, "/api/customer/checkout", request);
        Assert.Equal(HttpStatusCode.Created, purchased.StatusCode);
        Assert.Equal("no-store", purchased.Headers.CacheControl!.ToString());
        var receipt = (await purchased.Content.ReadFromJsonAsync<PurchaseReceipt>())!;
        Assert.Equal(HttpStatusCode.OK, (await Post(buyer, "/api/customer/checkout", request)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/customer/orders/{receipt.OrderId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/customer/checkout/attempts/{request.IdempotencyKey}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await buyer.GetAsync($"/api/customer/checkout/attempts/{request.IdempotencyKey}")).StatusCode);
        var list = (await manager.GetFromJsonAsync<DeliveryPage>("/api/product-management/deliveries"))!;
        var delivery = list.Items.Single();
        Assert.Equal(HttpStatusCode.BadRequest, (await manager.PostAsJsonAsync($"/api/product-management/deliveries/{delivery.Id}/status", new DeliveryTransition("InTransit", delivery.Version))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(manager, $"/api/product-management/deliveries/{delivery.Id}/status", new DeliveryTransition("InTransit", delivery.Version))).StatusCode);
        var updated = await buyer.GetFromJsonAsync<PurchaseReceipt>($"/api/customer/orders/{receipt.OrderId}");
        Assert.Equal("InTransit", updated!.Status);
    }

    private async Task AssertUnchangedAsync(int stock)
    {
        await using var check = fixture.Create();
        Assert.Equal(stock, (await check.Products.SingleAsync()).StockQuantity);
        Assert.Equal(CartState.Open, (await check.Carts.SingleAsync()).State);
        Assert.Single(await check.CartItems.ToListAsync());
        Assert.Empty(await check.Orders.ToListAsync());
        Assert.Empty(await check.Payments.ToListAsync());
        Assert.Empty(await check.Deliveries.ToListAsync());
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
    private sealed class PurchaseApplication(MySqlFixture fixture) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:DefaultConnection", "Server=localhost;Database=unused;User ID=unused;Password=unused");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<StoreDbContext>();
                services.AddScoped(_ => fixture.Create());
            });
        }
    }
    private sealed class FailSave : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,
            InterceptionResult<int> result, CancellationToken ct = default) => throw new InvalidOperationException("Injected storage failure.");
    }
}
