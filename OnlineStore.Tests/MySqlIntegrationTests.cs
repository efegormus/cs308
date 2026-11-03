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
        await db.OrderItems.ExecuteDeleteAsync();
        await db.Orders.ExecuteDeleteAsync();
        await db.Products.ExecuteDeleteAsync();
        await db.Categories.ExecuteDeleteAsync();
        await db.Users.ExecuteDeleteAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

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
            attempt.Products.Add(new Product { Sku = field, Name = "Invalid", CategoryId = field == "category" ? int.MaxValue : category.Id,
                Price = field == "price" ? -1 : 1, StockQuantity = field == "stock" ? -1 : 1, Cost = field == "cost" ? -1 : null });
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
        var order = new Order { User = user,
            OrderItems = [new OrderItem { Product = product, ProductName = product.Name, UnitPrice = product.Price, Quantity = 2 }] };
        order.ConfirmPayment(DateTime.UtcNow);
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

    private static User NewUser(string email)
    {
        var user = new User { Name = "Test customer", Email = email, PasswordHash = "", TaxId = "TAX-TEST", HomeAddress = "10 Example Street" };
        user.PasswordHash = new Microsoft.AspNetCore.Identity.PasswordHasher<User>().HashPassword(user, "Integration test passphrase");
        return user;
    }
}
