using Microsoft.EntityFrameworkCore;
using OnlineStore.Catalog;
using OnlineStore.Data;
using OnlineStore.Models;
using OnlineStore.Pricing;

namespace OnlineStore.Tests;

// SCRUM-83: a new product (price 0) stays hidden from shoppers until the sales manager sets a price.
public class UnpricedProductTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static StoreDbContext NewDb() => new(new DbContextOptionsBuilder<StoreDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<User> AddUserAsync(StoreDbContext db, UserRole role)
    {
        var user = new User { Name = "User", Email = $"{Guid.NewGuid():N}@example.com", PasswordHash = "hash",
            TaxId = "-", HomeAddress = "-", Role = role };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task<Product> AddProductAsync(StoreDbContext db, decimal price)
    {
        var product = new Product { Sku = "UNP-" + Guid.NewGuid().ToString("N"), Name = "Phone " + price, Price = price,
            StockQuantity = 5, IsActive = true, Category = new Category { Name = "Unpriced " + Guid.NewGuid().ToString("N") } };
        CatalogFixture.CompleteDetails(product);
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product;
    }

    private static Task<List<long>> ShopperIds(StoreDbContext db) =>
        ProductPricing.Query(db, Now.UtcDateTime).Select(p => p.Product.Id).ToListAsync();

    [Fact]
    public async Task AProductWithoutAPrice_IsHiddenFromShoppingReads()
    {
        await using var db = NewDb();
        var priced = await AddProductAsync(db, 100m);
        await AddProductAsync(db, 0m);

        Assert.Equal([priced.Id], await ShopperIds(db));
    }

    [Fact]
    public async Task TheSalesManagersQuery_CanStillSeeUnpricedProducts()
    {
        await using var db = NewDb();
        var priced = await AddProductAsync(db, 100m);
        var unpriced = await AddProductAsync(db, 0m);

        var ids = await ProductPricing.Query(db, Now.UtcDateTime, includeUnpriced: true).Select(p => p.Product.Id).ToListAsync();

        Assert.Equal([priced.Id, unpriced.Id], ids.Order());
    }

    [Fact]
    public async Task TheSalesProductList_ShowsUnpricedProductsWithAListPriceOfZero()
    {
        await using var db = NewDb();
        var manager = await AddUserAsync(db, UserRole.SalesManager);
        var unpriced = await AddProductAsync(db, 0m);

        var page = await new PricingService(db, new FixedClock(Now)).ListProductsAsync(manager.Id, 0, default);

        Assert.Equal(0m, Assert.Single(page.Products, p => p.Id == unpriced.Id).ListPrice);
    }

    [Fact]
    public async Task SettingAPrice_MakesTheProductVisibleToShoppers()
    {
        await using var db = NewDb();
        var manager = await AddUserAsync(db, UserRole.SalesManager);
        var product = await AddProductAsync(db, 0m);
        Assert.Empty(await ShopperIds(db));

        await new PricingService(db, new FixedClock(Now)).SetPriceAsync(manager.Id, product.Id, new PriceChange(149.90m), default);

        Assert.Equal([product.Id], await ShopperIds(db));
    }

    [Fact]
    public async Task ADiscountOnAPricedProduct_ShowsTheNewPriceToShoppers()
    {
        await using var db = NewDb();
        var manager = await AddUserAsync(db, UserRole.SalesManager);
        var product = await AddProductAsync(db, 200m);
        var service = new PricingService(db, new FixedClock(Now));

        await service.CreateDiscountAsync(manager.Id,
            new DiscountRequest("Sale", .25m, null, Now.AddDays(1), [product.Id]), default);

        var shown = await ProductPricing.Query(db, Now.UtcDateTime).SingleAsync(p => p.Product.Id == product.Id);
        Assert.Equal(200m, shown.ListPrice);
        Assert.Equal(150m, shown.Price);
        Assert.Equal(.25m, shown.DiscountRate);
    }

    [Theory]
    [InlineData(100, .2, 80)]
    [InlineData(19.99, .15, 16.99)]
    [InlineData(0.05, .5, 0.03)]
    [InlineData(10.01, .5, 5.01)]
    [InlineData(100, .0001, 99.99)]
    public void TheDiscountedPrice_RoundsHalfACentUp(double list, double rate, double expected) =>
        Assert.Equal((decimal)expected, ProductPricing.Calculate((decimal)list, (decimal)rate));

    [Fact]
    public async Task OnlySalesManagersCanSetPrices()
    {
        await using var db = NewDb();
        var customer = await AddUserAsync(db, UserRole.Customer);
        var product = await AddProductAsync(db, 0m);

        var error = await Assert.ThrowsAsync<OnlineStore.Purchasing.PurchaseException>(() =>
            new PricingService(db, new FixedClock(Now)).SetPriceAsync(customer.Id, product.Id, new PriceChange(10m), default));

        Assert.Equal(403, error.Status);
        Assert.Equal(0m, (await db.Products.AsNoTracking().SingleAsync(p => p.Id == product.Id)).Price);
    }
}
