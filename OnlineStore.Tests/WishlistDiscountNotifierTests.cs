using Microsoft.EntityFrameworkCore;
using OnlineStore.Data;
using OnlineStore.Models;
using OnlineStore.Pricing;

namespace OnlineStore.Tests;

// SCRUM-85: wishlist customers are notified when the sales manager discounts a product (in-memory database,
// mocked email sender).
public class WishlistDiscountNotifierTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class RecordingSender : IDiscountEmailSender
    {
        public List<DiscountEmailMessage> Sent { get; } = [];
        public Task SendAsync(DiscountEmailMessage message, CancellationToken ct) { Sent.Add(message); return Task.CompletedTask; }
    }

    private sealed class FailingSender : IDiscountEmailSender
    {
        public Task SendAsync(DiscountEmailMessage message, CancellationToken ct) => throw new InvalidOperationException("smtp down");
    }

    private static StoreDbContext NewDb() => new(new DbContextOptionsBuilder<StoreDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<User> AddUserAsync(StoreDbContext db, UserRole role = UserRole.Customer, bool active = true)
    {
        var user = new User { Name = "User", Email = $"{Guid.NewGuid():N}@example.com", PasswordHash = "hash",
            TaxId = "-", HomeAddress = "-", Role = role, IsActive = active };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task<List<Product>> AddProductsAsync(StoreDbContext db, int count)
    {
        var category = new Category { Name = "Wish " + Guid.NewGuid().ToString("N") };
        var products = new List<Product>();
        for (var i = 0; i < count; i++)
        {
            var product = new Product { Sku = "WSH-" + Guid.NewGuid().ToString("N"), Name = $"Phone {i}", Price = 100,
                StockQuantity = 5, IsActive = true, Category = category };
            CatalogFixture.CompleteDetails(product);
            products.Add(product);
        }
        db.Products.AddRange(products);
        await db.SaveChangesAsync();
        return products;
    }

    private static async Task WishAsync(StoreDbContext db, User user, params Product[] products)
    {
        db.WishlistItems.AddRange(products.Select(p => new WishlistItem { UserId = user.Id, ProductId = p.Id }));
        await db.SaveChangesAsync();
    }

    private static (PricingService Service, WishlistDiscountNotifier Notifier) Build(StoreDbContext db, IDiscountEmailSender sender)
    {
        var notifier = new WishlistDiscountNotifier(db, sender);
        return (new PricingService(db, new FixedClock(Now), notifier), notifier);
    }

    private static DiscountRequest Request(IEnumerable<long> ids, decimal rate = .2m) =>
        new("Black Friday", rate, null, Now.AddDays(3), ids.ToArray());

    [Fact]
    public async Task CreatingADiscount_EmailsEachCustomerWhoWishlistedTheProduct()
    {
        await using var db = NewDb();
        var manager = await AddUserAsync(db, UserRole.SalesManager);
        var a = await AddUserAsync(db);
        var b = await AddUserAsync(db);
        var products = await AddProductsAsync(db, 1);
        await WishAsync(db, a, products[0]);
        await WishAsync(db, b, products[0]);
        var sender = new RecordingSender();

        await Build(db, sender).Service.CreateDiscountAsync(manager.Id, Request([products[0].Id]), default);

        Assert.Equal(2, sender.Sent.Count);
        Assert.Contains(sender.Sent, m => m.Recipient == a.Email);
        Assert.Contains(sender.Sent, m => m.Recipient == b.Email);
        Assert.All(sender.Sent, m => Assert.Contains("20% off", m.Body));
    }

    [Fact]
    public async Task CreatingADiscount_DoesNotEmailCustomersWhoDidNotWishlistIt()
    {
        await using var db = NewDb();
        var manager = await AddUserAsync(db, UserRole.SalesManager);
        var bystander = await AddUserAsync(db);
        var products = await AddProductsAsync(db, 2);
        await WishAsync(db, bystander, products[1]);
        var sender = new RecordingSender();

        await Build(db, sender).Service.CreateDiscountAsync(manager.Id, Request([products[0].Id]), default);

        Assert.Empty(sender.Sent);
        Assert.Empty(await db.Notifications.ToListAsync());
    }

    [Fact]
    public async Task OneCustomerWithSeveralDiscountedProducts_GetsOneEmailAndOneNotificationPerProduct()
    {
        await using var db = NewDb();
        var manager = await AddUserAsync(db, UserRole.SalesManager);
        var customer = await AddUserAsync(db);
        var products = await AddProductsAsync(db, 3);
        await WishAsync(db, customer, products.ToArray());
        var sender = new RecordingSender();

        await Build(db, sender).Service.CreateDiscountAsync(manager.Id, Request(products.Select(p => p.Id)), default);

        var mail = Assert.Single(sender.Sent);
        Assert.All(products, p => Assert.Contains(p.Name, mail.Body));
        Assert.Equal(3, await db.Notifications.CountAsync(n => n.UserId == customer.Id));
    }

    [Fact]
    public async Task StoresAWishlistDiscountNotificationWithTheDiscountAndProduct()
    {
        await using var db = NewDb();
        var manager = await AddUserAsync(db, UserRole.SalesManager);
        var customer = await AddUserAsync(db);
        var product = (await AddProductsAsync(db, 1))[0];
        await WishAsync(db, customer, product);

        var discount = await Build(db, new RecordingSender()).Service
            .CreateDiscountAsync(manager.Id, Request([product.Id]), default);

        var n = await db.Notifications.SingleAsync();
        Assert.Equal(NotificationKind.WishlistDiscount, n.Kind);
        Assert.Equal(customer.Id, n.UserId);
        Assert.Equal(product.Id, n.ProductId);
        Assert.Equal(discount.Id, n.DiscountId);
        Assert.Equal(WishlistDiscountNotifier.Key(discount.Id, product.Id, customer.Id), n.DeduplicationKey);
    }

    [Fact]
    public async Task RunningTheNotifierTwice_SendsAndStoresNothingTheSecondTime()
    {
        await using var db = NewDb();
        var manager = await AddUserAsync(db, UserRole.SalesManager);
        var customer = await AddUserAsync(db);
        var product = (await AddProductsAsync(db, 1))[0];
        await WishAsync(db, customer, product);
        var sender = new RecordingSender();
        var (service, notifier) = Build(db, sender);
        var discount = await service.CreateDiscountAsync(manager.Id, Request([product.Id]), default);

        var second = await notifier.NotifyAsync(discount.Id, default);

        Assert.Equal(0, second);
        Assert.Single(sender.Sent);
        Assert.Equal(1, await db.Notifications.CountAsync());
    }

    [Fact]
    public async Task InactiveCustomers_AreSkipped()
    {
        await using var db = NewDb();
        var manager = await AddUserAsync(db, UserRole.SalesManager);
        var gone = await AddUserAsync(db, active: false);
        var product = (await AddProductsAsync(db, 1))[0];
        await WishAsync(db, gone, product);
        var sender = new RecordingSender();

        await Build(db, sender).Service.CreateDiscountAsync(manager.Id, Request([product.Id]), default);

        Assert.Empty(sender.Sent);
        Assert.Empty(await db.Notifications.ToListAsync());
    }

    [Fact]
    public async Task AFailingEmailSender_DoesNotFailTheDiscountAndKeepsTheNotification()
    {
        await using var db = NewDb();
        var manager = await AddUserAsync(db, UserRole.SalesManager);
        var customer = await AddUserAsync(db);
        var product = (await AddProductsAsync(db, 1))[0];
        await WishAsync(db, customer, product);

        var discount = await Build(db, new FailingSender()).Service.CreateDiscountAsync(manager.Id, Request([product.Id]), default);

        Assert.True(discount.Id > 0);
        Assert.Equal(1, await db.Discounts.CountAsync());
        Assert.Equal(1, await db.Notifications.CountAsync());
    }

    [Fact]
    public async Task APricingServiceWithoutANotifier_StillCreatesTheDiscount()
    {
        await using var db = NewDb();
        var manager = await AddUserAsync(db, UserRole.SalesManager);
        var product = (await AddProductsAsync(db, 1))[0];

        var discount = await new PricingService(db, new FixedClock(Now)).CreateDiscountAsync(manager.Id, Request([product.Id]), default);

        Assert.True(discount.Id > 0);
    }

    [Fact]
    public async Task AnInvalidDiscount_SendsNothing()
    {
        await using var db = NewDb();
        var manager = await AddUserAsync(db, UserRole.SalesManager);
        var customer = await AddUserAsync(db);
        var product = (await AddProductsAsync(db, 1))[0];
        await WishAsync(db, customer, product);
        var sender = new RecordingSender();

        await Assert.ThrowsAsync<OnlineStore.Purchasing.PurchaseException>(() =>
            Build(db, sender).Service.CreateDiscountAsync(manager.Id, Request([product.Id], rate: 1.5m), default));

        Assert.Empty(sender.Sent);
    }

    [Fact]
    public async Task LongProductLists_AreCappedInTheEmail()
    {
        await using var db = NewDb();
        var manager = await AddUserAsync(db, UserRole.SalesManager);
        var customer = await AddUserAsync(db);
        var products = await AddProductsAsync(db, 12);
        await WishAsync(db, customer, products.ToArray());
        var sender = new RecordingSender();

        await Build(db, sender).Service.CreateDiscountAsync(manager.Id, Request(products.Select(p => p.Id)), default);

        Assert.Contains("and 2 more", Assert.Single(sender.Sent).Body);
    }

    [Fact]
    public async Task TheLocalSink_KeepsOnlyTheLatestMessages()
    {
        var sink = new LocalDiscountEmailSender();
        for (var i = 0; i < LocalDiscountEmailSender.RetentionLimit + 5; i++)
            await sink.SendAsync(new DiscountEmailMessage($"u{i}@example.com", "s", "b", $"<{i}>"), default);

        Assert.Equal(LocalDiscountEmailSender.RetentionLimit, sink.Messages.Count);
        Assert.Equal("u5@example.com", sink.Messages[0].Recipient);
    }

    [Fact]
    public async Task TheSmtpSender_RejectsAnInvalidRecipientBeforeConnecting()
    {
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new SmtpDiscountEmailSender(config).SendAsync(new DiscountEmailMessage("not-an-email", "s", "b", "<1>"), default));
    }
}
