using Microsoft.EntityFrameworkCore;
using OnlineStore.Data;
using OnlineStore.Models;
using OnlineStore.Purchasing;
using OnlineStore.Reviews;

namespace OnlineStore.Tests;

public class ModerationTests
{
    [Theory]
    [InlineData("Approved",1)]
    [InlineData("Rejected",0)]
    public async Task DecisionsControlPublicTextWithoutChangingTheRating(string status, int visible)
    {
        await using var db = new StoreDbContext(new DbContextOptionsBuilder<StoreDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var g = await ReviewFixture.SeedAsync(db); g.Other.Role = UserRole.ProductManager; await db.SaveChangesAsync();
        var service = new ReviewService(db, TimeProvider.System);
        await service.SubmitAsync(g.Buyer.Id, g.Product.Id, new(5,"Review text"), default);
        var pending = Assert.Single((await new ModerationService(db, TimeProvider.System).ListAsync(null, 0, default)).Comments);
        var result = await service.ModerateAsync(g.Other.Id, pending.Id, new(status,pending.Version),default);
        Assert.Equal(status,result.Status); Assert.NotEqual(pending.Version,result.Version);
        Assert.Equal(visible,(await service.PublicAsync(g.Product.Id,0,default)).Comments.Count);
        Assert.Equal(5d,(await service.PublicAsync(g.Product.Id,0,default)).AverageRating);
        Assert.Equal(status,(await service.OwnAsync(g.Buyer.Id,g.Product.Id,default)).Comment!.Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Pending")]
    [InlineData("approved")]
    [InlineData("Anything")]
    public async Task InvalidDecisionsCannotChangeAComment(string? decision)
    {
        await using var db = new StoreDbContext(new DbContextOptionsBuilder<StoreDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var service = new ReviewService(db,TimeProvider.System);
        Assert.Equal(400,(await Assert.ThrowsAsync<PurchaseException>(()=>service.ModerateAsync(1,1,new(decision,"1"),default))).Status);
    }
}
