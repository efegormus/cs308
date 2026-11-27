using Microsoft.EntityFrameworkCore;
using OnlineStore.Data;
using OnlineStore.Models;
using OnlineStore.Pricing;
using OnlineStore.Purchasing;

namespace OnlineStore.Tests;

// SCRUM-155: sales manager pricing and discount API (service level, in-memory database).
public class PricingServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static StoreDbContext NewDb() => new(new DbContextOptionsBuilder<StoreDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static PricingService NewService(StoreDbContext db) => new(db, new FixedClock(Now));

    private static async Task<User> AddUserAsync(StoreDbContext db, UserRole role = UserRole.SalesManager, bool active = true)
    {
        var user = new User { Name = "User", Email = $"{Guid.NewGuid():N}@example.com", PasswordHash = "hash",
            TaxId = "-", HomeAddress = "-", Role = role, IsActive = active };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task<List<Product>> AddProductsAsync(StoreDbContext db, int count, decimal price = 100, bool active = true)
    {
        var category = new Category { Name = "Pricing " + Guid.NewGuid().ToString("N") };
        var products = new List<Product>();
        for (var i = 0; i < count; i++)
        {
            var product = new Product { Sku = "PRC-" + Guid.NewGuid().ToString("N"), Name = $"Product {i}", Price = price,
                StockQuantity = 5, IsActive = active, Category = category };
            CatalogFixture.CompleteDetails(product);
            products.Add(product);
        }
        db.Products.AddRange(products);
        await db.SaveChangesAsync();
        return products;
    }

    private static DiscountRequest Request(IEnumerable<long> productIds, decimal rate = .2m, DateTimeOffset? starts = null,
        DateTimeOffset? ends = null) => new("Black Friday", rate, starts, ends ?? Now.AddDays(3), productIds.ToArray());

    private static async Task<PurchaseException> Fails(Func<Task> action) => await Assert.ThrowsAsync<PurchaseException>(action);

    // ----- request validation -----

    [Theory]
    [InlineData(0d, false)]
    [InlineData(-5d, false)]
    [InlineData(10000000d, false)]
    [InlineData(10.123d, false)]
    [InlineData(0.01d, true)]
    [InlineData(99.5d, true)]
    [InlineData(9999999.99d, true)]
    public void PriceChange_ValidatesTheNewPrice(double price, bool valid) =>
        Assert.Equal(valid, new PriceChange((decimal)price).Validate().Count == 0);

    [Fact]
    public void PriceChange_RequiresAPrice() => Assert.Contains("price", new PriceChange(null).Validate().Keys);

    [Theory]
    [InlineData(0d, false)]
    [InlineData(1d, false)]
    [InlineData(-0.1d, false)]
    [InlineData(0.12345d, false)]
    [InlineData(0.15d, true)]
    [InlineData(0.9999d, true)]
    public void DiscountRequest_ValidatesTheRate(double rate, bool valid) =>
        Assert.Equal(valid, !Request([1L], (decimal)rate).Validate().ContainsKey("rate"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DiscountRequest_RequiresAName(string? name) =>
        Assert.Contains("name", new DiscountRequest(name, .1m, null, Now.AddDays(1), [1L]).Validate().Keys);

    [Fact]
    public void DiscountRequest_BoundsTheNameLength() =>
        Assert.Contains("name", new DiscountRequest(new string('x', 101), .1m, null, Now.AddDays(1), [1L]).Validate().Keys);

    [Fact]
    public void DiscountRequest_RequiresAnEndDate() =>
        Assert.Contains("endsAt", new DiscountRequest("Sale", .1m, null, null, [1L]).Validate().Keys);

    [Fact]
    public void DiscountRequest_RejectsAnEndDateBeforeTheStartDate() =>
        Assert.Contains("endsAt", new DiscountRequest("Sale", .1m, Now.AddDays(2), Now.AddDays(1), [1L]).Validate().Keys);

    [Theory]
    [InlineData(null)]
    [InlineData(new long[0])]
    [InlineData(new long[] { 0 })]
    [InlineData(new long[] { -3 })]
    [InlineData(new long[] { 4, 4 })]
    public void DiscountRequest_RequiresDistinctPositiveProductIds(long[]? ids) =>
        Assert.Contains("productIds", new DiscountRequest("Sale", .1m, null, Now.AddDays(1), ids).Validate().Keys);

    [Fact]
    public void DiscountRequest_BoundsTheNumberOfProducts() =>
        Assert.Contains("productIds", Request(Enumerable.Range(1, DiscountRequest.MaxProducts + 1).Select(i => (long)i))
            .Validate().Keys);

    [Fact]
    public void DiscountRequest_AcceptsAValidCampaign() => Assert.Empty(Request([1L, 2L]).Validate());

    // ----- role guard -----

    [Theory]
    [InlineData(UserRole.Customer)]
    [InlineData(UserRole.ProductManager)]
    [InlineData(UserRole.SupportAgent)]
    public async Task Service_RefusesEveryOtherRole(UserRole role)
    {
        await using var db = NewDb();
        var user = await AddUserAsync(db, role);
        Assert.Equal(403, (await Fails(() => NewService(db).ListProductsAsync(user.Id, 0, default))).Status);
        Assert.Equal(403, (await Fails(() => NewService(db).ListDiscountsAsync(user.Id, 0, default))).Status);
        Assert.Equal(403, (await Fails(() => NewService(db).CancelDiscountAsync(user.Id, 1, default))).Status);
    }

    [Fact]
    public async Task Service_AsksUnknownOrInactiveUsersToLogInAgain()
    {
        await using var db = NewDb();
        var inactive = await AddUserAsync(db, active: false);
        Assert.Equal(401, (await Fails(() => NewService(db).ListProductsAsync(inactive.Id, 0, default))).Status);
        Assert.Equal(401, (await Fails(() => NewService(db).ListProductsAsync(9999, 0, default))).Status);
    }

    // ----- product list and list price -----

    [Fact]
    public async Task ListProducts_ShowsListPriceAndEffectivePrice()
    {
        await using var db = NewDb();
        var manager = await AddUserAsync(db);
        var products = await AddProductsAsync(db, 2);
        await NewService(db).CreateDiscountAsync(manager.Id, Request([products[0].Id]), default);

        var page = await NewService(db).ListProductsAsync(manager.Id, 0, default);
        var discounted = page.Products.Single(p => p.Id == products[0].Id);
        var regular = page.Products.Single(p => p.Id == products[1].Id);
        Assert.Equal(100m, discounted.ListPrice);
        Assert.Equal(80m, discounted.Price);
        Assert.Equal(.2m, discounted.DiscountRate);
        Assert.Equal(100m, regular.Price);
        Assert.Equal(0m, regular.DiscountRate);
    }

    [Fact]
    public async Task ListProducts_PagesByProductId()
    {
        await using var db = NewDb();
        var manager = await AddUserAsync(db);
        await AddProductsAsync(db, PricingService.PageSize + 1);
        var first = await NewService(db).ListProductsAsync(manager.Id, 0, default);
        Assert.Equal(PricingService.PageSize, first.Products.Count);
        Assert.NotNull(first.NextAfterId);
        var second = await NewService(db).ListProductsAsync(manager.Id, first.NextAfterId!.Value, default);
        Assert.Single(second.Products);
        Assert.Null(second.NextAfterId);
    }

    [Fact]
    public async Task ListProducts_RejectsANegativeCursor()
    {
        await using var db = NewDb();
        var manager = await AddUserAsync(db);
        Assert.Equal(400, (await Fails(() => NewService(db).ListProductsAsync(manager.Id, -1, default))).Status);
    }

    [Fact]
    public async Task SetPrice_ChangesTheListPrice()
    {
        await using var db = NewDb();
        var manager = await AddUserAsync(db);
        var product = (await AddProductsAsync(db, 1))[0];
        var result = await NewService(db).SetPriceAsync(manager.Id, product.Id, new PriceChange(149.90m), default);
        Assert.Equal(149.90m, result.ListPrice);
        Assert.Equal(149.90m, result.Price);
        Assert.Equal(149.90m, (await db.Products.AsNoTracking().SingleAsync(p => p.Id == product.Id)).Price);
    }

    [Fact]
    public async Task SetPrice_AppliesAnActiveDiscountToTheNewListPrice()
    {
        await using var db = NewDb();
        var manager = await AddUserAsync(db);
        var product = (await AddProductsAsync(db, 1))[0];
        await NewService(db).CreateDiscountAsync(manager.Id, Request([product.Id], .25m), default);
        var result = await NewService(db).SetPriceAsync(manager.Id, product.Id, new PriceChange(200m), default);
        Assert.Equal(200m, result.ListPrice);
        Assert.Equal(150m, result.Price);
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(-1d)]
    public async Task SetPrice_RejectsAnInvalidPrice(double price)
    {
        await using var db = NewDb();
        var manager = await AddUserAsync(db);
        var product = (await AddProductsAsync(db, 1))[0];
        Assert.Equal(400, (await Fails(() => NewService(db)
            .SetPriceAsync(manager.Id, product.Id, new PriceChange((decimal)price), default))).Status);
        Assert.Equal(100m, (await db.Products.AsNoTracking().SingleAsync(p => p.Id == product.Id)).Price);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    [InlineData(9999)]
    public async Task SetPrice_ReportsAnUnknownProduct(long productId)
    {
        await using var db = NewDb();
        var manager = await AddUserAsync(db);
        Assert.Equal(404, (await Fails(() => NewService(db)
            .SetPriceAsync(manager.Id, productId, new PriceChange(10m), default))).Status);
    }

    // ----- creating a discount -----

    [Fact]
    public async Task CreateDiscount_StartsNowWhenNoStartIsGiven()
    {
        await using var db = NewDb();
        var manager = await AddUserAsync(db);
        var products = await AddProductsAsync(db, 2);
        var view = await NewService(db).CreateDiscountAsync(manager.Id, Request(products.Select(p => p.Id)), default);
        Assert.Equal("Active", view.Status);
        Assert.Equal(Now.UtcDateTime, view.StartsAt);
        Assert.Equal(Now.AddDays(3).UtcDateTime, view.EndsAt);
        Assert.Equal(products.Select(p => p.Id).OrderBy(id => id), view.ProductIds);
        var stored = await db.Discounts.AsNoTracking().SingleAsync();
        Assert.Equal(manager.Id, stored.CreatedByUserId);
        Assert.Equal(2, await db.DiscountProducts.CountAsync());
    }

    [Fact]
    public async Task CreateDiscount_CanBeScheduledForTheFuture()
    {
        await using var db = NewDb();
        var manager = await AddUserAsync(db);
        var product = (await AddProductsAsync(db, 1))[0];
        var view = await NewService(db).CreateDiscountAsync(manager.Id,
            Request([product.Id], starts: Now.AddDays(1), ends: Now.AddDays(2)), default);
        Assert.Equal("Scheduled", view.Status);
        // It does not change today's price.
        var page = await NewService(db).ListProductsAsync(manager.Id, 0, default);
        Assert.Equal(100m, page.Products.Single().Price);
    }

    [Fact]
    public async Task CreateDiscount_TrimsTheName()
    {
        await using var db = NewDb();
        var manager = await AddUserAsync(db);
        var product = (await AddProductsAsync(db, 1))[0];
        var view = await NewService(db).CreateDiscountAsync(manager.Id,
            new("  Weekend  ", .1m, null, Now.AddDays(1), [product.Id]), default);
        Assert.Equal("Weekend", view.Name);
    }

    [Fact]
    public async Task CreateDiscount_RejectsAnUnknownProductAndStoresNothing()
    {
        await using var db = NewDb();
        var manager = await AddUserAsync(db);
        var product = (await AddProductsAsync(db, 1))[0];
        Assert.Equal(400, (await Fails(() => NewService(db)
            .CreateDiscountAsync(manager.Id, Request([product.Id, 9999]), default))).Status);
        Assert.Empty(db.Discounts);
        Assert.Empty(db.DiscountProducts);
    }

    [Fact]
    public async Task CreateDiscount_RejectsAnInactiveProduct()
    {
        await using var db = NewDb();
        var manager = await AddUserAsync(db);
        var product = (await AddProductsAsync(db, 1, active: false))[0];
        Assert.Equal(400, (await Fails(() => NewService(db)
            .CreateDiscountAsync(manager.Id, Request([product.Id]), default))).Status);
    }

    [Fact]
    public async Task CreateDiscount_RejectsACampaignThatAlreadyEnded()
    {
        await using var db = NewDb();
        var manager = await AddUserAsync(db);
        var product = (await AddProductsAsync(db, 1))[0];
        var error = await Fails(() => NewService(db).CreateDiscountAsync(manager.Id,
            Request([product.Id], starts: Now.AddDays(-3), ends: Now.AddDays(-1)), default));
        Assert.Equal(400, error.Status);
    }

    [Fact]
    public async Task CreateDiscount_RejectsAnEndAtOrBeforeTheDefaultStart()
    {
        await using var db = NewDb();
        var manager = await AddUserAsync(db);
        var product = (await AddProductsAsync(db, 1))[0];
        var error = await Fails(() => NewService(db).CreateDiscountAsync(manager.Id,
            Request([product.Id], ends: Now), default));
        Assert.Equal(400, error.Status);
    }

    [Fact]
    public async Task CreateDiscount_TheHighestOverlappingRateWins()
    {
        await using var db = NewDb();
        var manager = await AddUserAsync(db);
        var product = (await AddProductsAsync(db, 1))[0];
        await NewService(db).CreateDiscountAsync(manager.Id, Request([product.Id], .1m), default);
        await NewService(db).CreateDiscountAsync(manager.Id, Request([product.Id], .3m), default);
        var page = await NewService(db).ListProductsAsync(manager.Id, 0, default);
        Assert.Equal(70m, page.Products.Single().Price);
        Assert.Equal(.3m, page.Products.Single().DiscountRate);
    }

    [Fact]
    public async Task CreateDiscount_LeavesTheListPriceAlone()
    {
        await using var db = NewDb();
        var manager = await AddUserAsync(db);
        var product = (await AddProductsAsync(db, 1))[0];
        await NewService(db).CreateDiscountAsync(manager.Id, Request([product.Id], .5m), default);
        Assert.Equal(100m, (await db.Products.AsNoTracking().SingleAsync(p => p.Id == product.Id)).Price);
    }

    // ----- cancelling and listing -----

    [Fact]
    public async Task CancelDiscount_RestoresTheListPrice()
    {
        await using var db = NewDb();
        var manager = await AddUserAsync(db);
        var product = (await AddProductsAsync(db, 1))[0];
        var created = await NewService(db).CreateDiscountAsync(manager.Id, Request([product.Id]), default);
        var cancelled = await NewService(db).CancelDiscountAsync(manager.Id, created.Id, default);
        Assert.Equal("Cancelled", cancelled.Status);
        Assert.NotNull(cancelled.CancelledAt);
        var page = await NewService(db).ListProductsAsync(manager.Id, 0, default);
        Assert.Equal(100m, page.Products.Single().Price);
    }

    [Fact]
    public async Task CancelDiscount_IsIdempotent()
    {
        await using var db = NewDb();
        var manager = await AddUserAsync(db);
        var product = (await AddProductsAsync(db, 1))[0];
        var created = await NewService(db).CreateDiscountAsync(manager.Id, Request([product.Id]), default);
        var first = await NewService(db).CancelDiscountAsync(manager.Id, created.Id, default);
        var later = new PricingService(db, new FixedClock(Now.AddHours(5)));
        var second = await later.CancelDiscountAsync(manager.Id, created.Id, default);
        Assert.Equal(first.CancelledAt, second.CancelledAt);
        Assert.Equal("Cancelled", second.Status);
    }

    [Fact]
    public async Task CancelDiscount_LeavesAnEndedCampaignExpired()
    {
        await using var db = NewDb();
        var manager = await AddUserAsync(db);
        var product = (await AddProductsAsync(db, 1))[0];
        var created = await NewService(db).CreateDiscountAsync(manager.Id, Request([product.Id]), default);
        var afterEnd = new PricingService(db, new FixedClock(Now.AddDays(10)));
        var result = await afterEnd.CancelDiscountAsync(manager.Id, created.Id, default);
        Assert.Equal("Expired", result.Status);
        Assert.Null(result.CancelledAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(9999)]
    public async Task CancelDiscount_ReportsAnUnknownDiscount(long id)
    {
        await using var db = NewDb();
        var manager = await AddUserAsync(db);
        Assert.Equal(404, (await Fails(() => NewService(db).CancelDiscountAsync(manager.Id, id, default))).Status);
    }

    [Fact]
    public async Task ListDiscounts_ShowsNewestFirstAndPages()
    {
        await using var db = NewDb();
        var manager = await AddUserAsync(db);
        var product = (await AddProductsAsync(db, 1))[0];
        for (var i = 0; i < PricingService.PageSize + 1; i++)
            await NewService(db).CreateDiscountAsync(manager.Id, Request([product.Id], .1m), default);
        var first = await NewService(db).ListDiscountsAsync(manager.Id, 0, default);
        Assert.Equal(PricingService.PageSize, first.Discounts.Count);
        Assert.True(first.Discounts[0].Id > first.Discounts[1].Id);
        Assert.NotNull(first.NextBeforeId);
        var second = await NewService(db).ListDiscountsAsync(manager.Id, first.NextBeforeId!.Value, default);
        Assert.Single(second.Discounts);
        Assert.Null(second.NextBeforeId);
    }

    [Fact]
    public async Task ListDiscounts_RejectsANegativeCursor()
    {
        await using var db = NewDb();
        var manager = await AddUserAsync(db);
        Assert.Equal(400, (await Fails(() => NewService(db).ListDiscountsAsync(manager.Id, -1, default))).Status);
    }

    [Theory]
    [InlineData(-1, 1, false, "Active")]
    [InlineData(1, 2, false, "Scheduled")]
    [InlineData(-3, -1, false, "Expired")]
    [InlineData(-1, 0, false, "Expired")]
    [InlineData(-1, 1, true, "Cancelled")]
    [InlineData(1, 2, true, "Cancelled")]
    public void StatusOf_ReadsTheClockAndTheCancellation(int startHours, int endHours, bool cancelled, string expected)
    {
        var now = Now.UtcDateTime;
        var status = PricingService.StatusOf(now.AddHours(startHours), now.AddHours(endHours),
            cancelled ? now.AddMinutes(-1) : null, now);
        Assert.Equal(expected, status);
    }
}
