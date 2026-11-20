using Microsoft.EntityFrameworkCore;
using OnlineStore.Catalog;
using OnlineStore.Data;
using OnlineStore.Models;

namespace OnlineStore.Tests;

public class ProductPricingTests
{
    [Theory]
    [InlineData(100, .1, 90)]
    [InlineData(100.05, .5, 50.03)]
    [InlineData(.01, .5, .01)]
    [InlineData(10.01, 0, 10.01)]
    [InlineData(100, 1, 0)]
    public void Price_RoundsNonnegativeMoneyAwayFromZero(decimal price, decimal rate, decimal expected) =>
        Assert.Equal(expected, ProductPricing.Calculate(price, rate));

    [Theory]
    [InlineData(-1, 1, false, .2)]
    [InlineData(0, 1, false, .2)]
    [InlineData(-1, 0, false, 0)]
    [InlineData(-2, -1, false, 0)]
    [InlineData(1, 2, false, 0)]
    [InlineData(-1, 1, true, 0)]
    public async Task Query_AppliesOnlyCurrentNonCancelledCampaigns(int start, int end, bool cancelled, decimal expectedRate)
    {
        await using var db = new StoreDbContext(new DbContextOptionsBuilder<StoreDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var now = new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);
        var product = new Product { Sku = "PRICE", Name = "Price", Price = 100, Category = new Category { Name = "Prices" } };
        CatalogFixture.CompleteDetails(product);
        db.Products.Add(product); await db.SaveChangesAsync();
        var discount = new Discount { Rate = .2m, StartsAt = now.AddHours(start), EndsAt = now.AddHours(end),
            CancelledAt = cancelled ? now.AddMinutes(-1) : null };
        db.DiscountProducts.Add(new DiscountProduct { ProductId = product.Id, Discount = discount });
        await db.SaveChangesAsync();
        var priced = await ProductPricing.Query(db, now).SingleAsync();
        Assert.Equal(expectedRate, priced.DiscountRate); Assert.Equal(100 * (1 - expectedRate), priced.Price);
        Assert.Equal(100, priced.ListPrice);
    }
}
