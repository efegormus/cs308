using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using OnlineStore.Data;
using OnlineStore.Models;
using OnlineStore.Seeding;

namespace OnlineStore.Tests;

public sealed class MySqlFactAttribute : FactAttribute
{
    public MySqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ONLINESTORE_TEST_MYSQL")))
            Skip = "Set ONLINESTORE_TEST_MYSQL to a disposable local database ending in _tests.";
    }
}

// These tests delete rows, so the fixture refuses every database except a local *_tests database.
public sealed class MySqlFixture : IAsyncLifetime
{
    private DbContextOptions<StoreDbContext>? options;

    public StoreDbContext Create() => new(options ?? throw new InvalidOperationException("MySQL fixture not configured."));

    public async Task InitializeAsync()
    {
        var connection = Environment.GetEnvironmentVariable("ONLINESTORE_TEST_MYSQL");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var parsed = new MySqlConnectionStringBuilder(connection) { DateTimeKind = MySqlDateTimeKind.Utc };
        if (parsed.Server is not ("127.0.0.1" or "localhost") || !parsed.Database.EndsWith("_tests", StringComparison.Ordinal))
            throw new InvalidOperationException("MySQL tests require a local disposable database ending in _tests.");
        options = new DbContextOptionsBuilder<StoreDbContext>()
            .UseMySql(parsed.ConnectionString, new MySqlServerVersion(new Version(8, 4, 13))).Options;
        await using var db = Create();
        await db.Database.MigrateAsync();
    }

    public async Task ResetAsync()
    {
        await using var db = Create();
        // Children before parents: payments, carts, comments and the rest would block deleting orders, products and users.
        await db.ChatAttachments.ExecuteDeleteAsync();
        await db.ChatMessages.ExecuteDeleteAsync();
        await db.Conversations.ExecuteDeleteAsync();
        await db.Refunds.ExecuteDeleteAsync();
        await db.ReturnRequests.ExecuteDeleteAsync();
        await db.Ratings.ExecuteDeleteAsync();
        await db.Comments.ExecuteDeleteAsync();
        await db.Notifications.ExecuteDeleteAsync();
        await db.WishlistItems.ExecuteDeleteAsync();
        await db.CartItems.ExecuteDeleteAsync();
        await db.Carts.ExecuteDeleteAsync();
        await db.DeliveryItems.ExecuteDeleteAsync();
        await db.Deliveries.ExecuteDeleteAsync();
        await db.Invoices.ExecuteDeleteAsync();
        await db.Payments.ExecuteDeleteAsync();
        await db.DiscountProducts.ExecuteDeleteAsync();
        await db.OrderItems.ExecuteDeleteAsync();
        await db.Discounts.ExecuteDeleteAsync();
        await db.Orders.ExecuteDeleteAsync();
        await db.Products.ExecuteDeleteAsync();
        await db.Categories.ExecuteDeleteAsync();
        await db.Users.ExecuteDeleteAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

// A purchased order must carry the customer data of that moment; the database requires it.
public static class PurchaseSnapshots
{
    public static Order WithCustomerSnapshot(this Order order, User user)
    {
        order.CustomerNameSnapshot = user.Name;
        order.TaxIdSnapshot = user.TaxId;
        order.EmailSnapshot = user.Email;
        order.AddressSnapshot = user.HomeAddress;
        return order;
    }
}

// The MySQL test classes share one database, so xUnit must not run them at the same time.
[CollectionDefinition("MySql")]
public sealed class MySqlCollection;

[Collection("MySql")]
public class MySqlIntegrationTests(MySqlFixture fixture) : IClassFixture<MySqlFixture>
{
    private static SeedCatalog SampleCatalog() => SeedCatalogReader.ReadFromFile(Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SeedData", "seed-products.json")));

    [MySqlFact]
    public async Task MigrationsAndSeed_RunOnRealMySqlAndPreserveExistingInventory()
    {
        await fixture.ResetAsync();
        await using var db = fixture.Create();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Equal(new SeedResult(2, 10), await SeedDataLoader.LoadAsync(db, SampleCatalog()));
        var product = await db.Products.FirstAsync();
        product.StockQuantity = 123;
        await db.SaveChangesAsync();
        Assert.Equal(new SeedResult(0, 0), await SeedDataLoader.LoadAsync(db, SampleCatalog()));
        Assert.Equal(123, await db.Products.Where(p => p.Id == product.Id).Select(p => p.StockQuantity).SingleAsync());
        Assert.Equal(2, await db.Categories.CountAsync());
        Assert.Equal(10, await db.Products.CountAsync());
    }

    [MySqlFact]
    public async Task MySql_EnforcesMoneyStockAndForeignKeys()
    {
        await fixture.ResetAsync();
        await using var db = fixture.Create();
        var category = new Category { Name = "Phones" };
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        foreach (var field in new[] { "price", "stock", "cost", "category" })
        {
            await using var attempt = fixture.Create();
            var product = new Product { Sku = field, Name = "Invalid", CategoryId = field == "category" ? int.MaxValue : category.Id,
                Price = field == "price" ? -1 : 1, StockQuantity = field == "stock" ? -1 : 1, Cost = field == "cost" ? -1 : null };
            CatalogFixture.CompleteDetails(product);
            attempt.Products.Add(product);
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => attempt.SaveChangesAsync());
            Assert.IsType<MySqlException>(error.InnerException);
        }
    }

    [MySqlFact]
    public async Task MySql_EnforcesEmailUniquenessCustomerFieldBoundsAndAllowedRoles()
    {
        await fixture.ResetAsync();
        await using var db = fixture.Create();
        var user = NewUser("customer@example.com");
        db.Users.Add(user);
        await db.SaveChangesAsync();
        await using (var duplicate = fixture.Create())
        {
            duplicate.Users.Add(NewUser("CUSTOMER@EXAMPLE.COM"));
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => duplicate.SaveChangesAsync());
            Assert.Equal(1062, Assert.IsType<MySqlException>(error.InnerException).Number);
        }
        await using (var overlong = fixture.Create())
        {
            var other = NewUser("other@example.com");
            other.TaxId = new string('x', 65);
            overlong.Users.Add(other);
            Assert.IsType<MySqlException>((await Assert.ThrowsAsync<DbUpdateException>(() => overlong.SaveChangesAsync())).InnerException);
        }
        await Assert.ThrowsAsync<MySqlException>(() => db.Database.ExecuteSqlRawAsync("UPDATE Users SET Role = 'customer'"));
        await Assert.ThrowsAsync<MySqlException>(() => db.Database.ExecuteSqlRawAsync("UPDATE Users SET Role = 'Administrator'"));
        await Assert.ThrowsAsync<MySqlException>(() => db.Database.ExecuteSqlRawAsync("UPDATE Users SET HomeAddress = NULL"));
        Assert.Equal(UserRole.Customer, await db.Users.AsNoTracking().Select(u => u.Role).SingleAsync());
    }

    [MySqlFact]
    public async Task MySql_PreservesPurchaseSnapshotsAndEnforcesDeletionRules()
    {
        await fixture.ResetAsync();
        await using var db = fixture.Create();
        var user = NewUser("buyer@example.com");
        var category = new Category { Name = "Phones" };
        var product = new Product { Sku = "PHONE", Name = "Original phone", Price = 10, Category = category };
        CatalogFixture.CompleteDetails(product);
        var order = new Order { User = user,
            OrderItems = [new OrderItem { Product = product, ProductName = product.Name, UnitPrice = product.Price, Quantity = 2 }] };
        order.WithCustomerSnapshot(user).ConfirmPayment(DateTime.UtcNow);
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        product.Name = "Renamed phone";
        product.Price = 99;
        await db.SaveChangesAsync();
        var line = await db.OrderItems.AsNoTracking().SingleAsync();
        Assert.Equal("Original phone", line.ProductName);
        Assert.Equal(10m, line.UnitPrice);
        foreach (var delete in new Func<Task<int>>[]
        {
            () => db.Products.Where(existing => existing.Id == product.Id).ExecuteDeleteAsync(),
            () => db.Categories.Where(existing => existing.Id == category.Id).ExecuteDeleteAsync(),
            () => db.Users.Where(existing => existing.Id == user.Id).ExecuteDeleteAsync()
        })
        {
            Assert.Equal(1451, (await Assert.ThrowsAsync<MySqlException>(delete)).Number);
        }
        // Bypass EF's tracked-entity cascade to test the actual database FK action.
        await db.Orders.Where(existing => existing.Id == order.Id).ExecuteDeleteAsync();
        Assert.Empty(await db.OrderItems.ToListAsync());
        Assert.Single(await db.Products.ToListAsync());
    }

    [MySqlFact]
    public async Task MySql_PersistsSeparatePaymentAndFulfillmentAndGuardsCatalogDetails()
    {
        await fixture.ResetAsync();
        await using var db = fixture.Create();
        var product = new Product { Sku = "SNAPSHOT", Name = "Snapshot phone", Price = 100, Cost = 40,
            StockQuantity = 5, Category = new Category { Name = "Phones" } };
        CatalogFixture.CompleteDetails(product);
        var purchasedAt = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var buyer = NewUser("snapshot@example.com");
        var order = new Order { User = buyer, OrderItems = [OrderItem.Snapshot(product, 2)] }.WithCustomerSnapshot(buyer);
        Assert.True(order.ConfirmPayment(purchasedAt));
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        Assert.True(order.TransitionTo(OrderStatus.InTransit, purchasedAt.AddDays(1)));
        Assert.True(order.TransitionTo(OrderStatus.Delivered, purchasedAt.AddDays(2)));
        await db.SaveChangesAsync();

        product.Price = 200;
        product.Cost = 90;
        await db.SaveChangesAsync();
        await using var check = fixture.Create();
        var persisted = await check.Orders.Include(value => value.OrderItems).SingleAsync();
        Assert.Equal(OrderStatus.Delivered, persisted.Status);
        Assert.Equal(PaymentStatus.Confirmed, persisted.PaymentStatus);
        Assert.Equal(purchasedAt, persisted.PurchasedAt);
        Assert.Equal(DateTimeKind.Utc, persisted.PurchasedAt!.Value.Kind);
        Assert.Equal(100, persisted.OrderItems.Single().UnitPrice);
        Assert.Equal(40, persisted.OrderItems.Single().UnitCost);
        Assert.True(persisted.CanRequestReturn(purchasedAt.AddDays(30)));
        Assert.False(persisted.CanRequestReturn(purchasedAt.AddDays(31)));
        Assert.Equal(3819, (await Assert.ThrowsAsync<MySqlException>(() =>
            check.Database.ExecuteSqlRawAsync("UPDATE Orders SET PaymentStatus = 'Pending' WHERE Status = 'Delivered'"))).Number);
        Assert.Equal(3819, (await Assert.ThrowsAsync<MySqlException>(() =>
            check.Database.ExecuteSqlRawAsync("UPDATE Products SET Description = NULL WHERE IsActive = 1"))).Number);
        await check.Database.ExecuteSqlRawAsync("UPDATE Products SET IsActive = 0, Description = NULL");
        Assert.Equal(3819, (await Assert.ThrowsAsync<MySqlException>(() =>
            check.Database.ExecuteSqlRawAsync("UPDATE Products SET IsActive = 1"))).Number);
    }

    private static User NewUser(string email)
    {
        var user = new User { Name = "Test customer", Email = email, PasswordHash = "", TaxId = "TAX-TEST", HomeAddress = "10 Example Street" };
        user.PasswordHash = new Microsoft.AspNetCore.Identity.PasswordHasher<User>().HashPassword(user, "Integration test passphrase");
        return user;
    }
}
