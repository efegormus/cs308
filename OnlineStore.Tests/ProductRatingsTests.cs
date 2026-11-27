using Microsoft.EntityFrameworkCore;
using OnlineStore.Catalog;
using OnlineStore.Data;
using OnlineStore.Dtos;
using OnlineStore.Models;

namespace OnlineStore.Tests;

// SCRUM-126: the rating shown on a product card is the public average of the product's ratings.
public class ProductRatingsTests
{
    private static StoreDbContext NewDb() => new(new DbContextOptionsBuilder<StoreDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static ProductListItemDto Item(long id) =>
        new(id, $"Phone {id}", 100m, 5, true, null, 1, "Phones", null, null, 100m, 0m, null, 0);

    private static async Task RateAsync(StoreDbContext db, long productId, params byte[] stars)
    {
        var user = 1000;
        foreach (var star in stars)
            db.Ratings.Add(new Rating { UserId = ++user, ProductId = productId, OrderItemId = user, Stars = star });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task AProductWithoutRatings_HasNoAverageAndACountOfZero()
    {
        await using var db = NewDb();

        var result = await ProductRatings.AttachAsync(db, [Item(1)], default);

        Assert.Null(result[0].AverageRating);
        Assert.Equal(0, result[0].RatingCount);
    }

    [Fact]
    public async Task TheAverageIsRoundedToOneDecimalAndTheCountIsExact()
    {
        await using var db = NewDb();
        await RateAsync(db, 1, 5, 4, 4); // 4.333...

        var result = await ProductRatings.AttachAsync(db, [Item(1)], default);

        Assert.Equal(4.3, result[0].AverageRating);
        Assert.Equal(3, result[0].RatingCount);
    }

    [Fact]
    public async Task EachProductGetsItsOwnRatings()
    {
        await using var db = NewDb();
        await RateAsync(db, 1, 5, 5);
        await RateAsync(db, 2, 1);

        var result = await ProductRatings.AttachAsync(db, [Item(1), Item(2), Item(3)], default);

        Assert.Equal([5.0, 1.0, null], result.Select(r => r.AverageRating));
        Assert.Equal([2, 1, 0], result.Select(r => r.RatingCount));
    }

    [Fact]
    public async Task OtherFieldsAndTheOrderOfTheItemsAreKept()
    {
        await using var db = NewDb();
        await RateAsync(db, 2, 3);
        var items = new List<ProductListItemDto> { Item(2), Item(1) };

        var result = await ProductRatings.AttachAsync(db, items, default);

        Assert.Equal([2L, 1L], result.Select(r => r.Id));
        Assert.Equal(items[0] with { AverageRating = 3.0, RatingCount = 1 }, result[0]);
        Assert.Equal(items[1], result[1]);
    }

    [Fact]
    public async Task AnEmptyPage_NeedsNoQueryAndReturnsTheSameList()
    {
        await using var db = NewDb();
        var empty = new List<ProductListItemDto>();

        Assert.Same(empty, await ProductRatings.AttachAsync(db, empty, default));
    }
}
