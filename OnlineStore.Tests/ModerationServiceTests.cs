using Microsoft.EntityFrameworkCore;
using OnlineStore.Data;
using OnlineStore.Models;
using OnlineStore.Purchasing;
using OnlineStore.Reviews;

namespace OnlineStore.Tests;

public class ModerationServiceTests
{
    private static StoreDbContext NewDb() => new(new DbContextOptionsBuilder<StoreDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<User> AddManagerAsync(StoreDbContext db, UserRole role = UserRole.ProductManager)
    {
        var manager = new User { Name = "Manager", Email = $"{Guid.NewGuid():N}@example.com", PasswordHash = "hash",
            TaxId = "-", HomeAddress = "-", Role = role };
        db.Users.Add(manager); await db.SaveChangesAsync();
        return manager;
    }

    private static async Task<(ReviewFixture.Graph Graph, User Manager, long CommentId)> SeedPendingAsync(StoreDbContext db)
    {
        var graph = await ReviewFixture.SeedAsync(db);
        var manager = await AddManagerAsync(db);
        var submitted = await new ReviewService(db, TimeProvider.System)
            .SubmitAsync(graph.Buyer.Id, graph.Product.Id, new(4, "Works well"), default);
        return (graph, manager, submitted.Review.Comment!.Id);
    }

    [Theory]
    [InlineData(null, null, false)]
    [InlineData("approve", null, false)]
    [InlineData("Maybe", null, false)]
    [InlineData("Approve", null, true)]
    [InlineData("Reject", "Spam", true)]
    public void Decision_ValidatesTheChoice(string? decision, string? note, bool valid) =>
        Assert.Equal(valid, new CommentDecision(decision, note).Validate().Count == 0);

    [Fact]
    public void Decision_BoundsTheNote() =>
        Assert.Contains("note", new CommentDecision("Reject", new string('x', 501)).Validate().Keys);

    [Fact]
    public async Task List_ShowsPendingCommentsWithoutPrivateAccountData()
    {
        await using var db = NewDb();
        var (graph, _, commentId) = await SeedPendingAsync(db);
        var page = await new ModerationService(db, TimeProvider.System).ListAsync(null, 0, default);
        Assert.Equal("Pending", page.Status);
        var item = Assert.Single(page.Comments);
        Assert.Equal(commentId, item.Id);
        Assert.Equal(graph.Product.Id, item.ProductId);
        Assert.Equal("Review product", item.ProductName);
        Assert.Equal("Buyer", item.AuthorName);
        Assert.Equal(4, item.Rating);
        Assert.Equal("Works well", item.Body);
        Assert.Null(page.NextAfterId);
        var json = System.Text.Json.JsonSerializer.Serialize(page);
        Assert.DoesNotContain("buyer@example.com", json);
        Assert.DoesNotContain("Private address", json);
        Assert.DoesNotContain("TAX", json);
    }

    [Theory]
    [InlineData("pending")]
    [InlineData("Unknown")]
    [InlineData("7")]
    [InlineData("0")]
    [InlineData("")]
    public async Task List_RejectsAnUnknownStatus(string status)
    {
        await using var db = NewDb();
        Assert.Equal(400, (await Assert.ThrowsAsync<PurchaseException>(() =>
            new ModerationService(db, TimeProvider.System).ListAsync(status, 0, default))).Status);
    }

    [Fact]
    public async Task List_RejectsANegativeCursor()
    {
        await using var db = NewDb();
        Assert.Equal(400, (await Assert.ThrowsAsync<PurchaseException>(() =>
            new ModerationService(db, TimeProvider.System).ListAsync("Pending", -1, default))).Status);
    }

    [Fact]
    public async Task List_PagesByAscendingIdAndFiltersByStatus()
    {
        await using var db = NewDb();
        var graph = await ReviewFixture.SeedAsync(db);
        var authors = Enumerable.Range(0, ModerationService.PageSize + 2).Select(i => new User
            { Name = $"Author {i}", Email = $"a{i}@example.com", PasswordHash = "hash", TaxId = "-", HomeAddress = "-" }).ToList();
        db.Users.AddRange(authors); await db.SaveChangesAsync();
        for (var i = 0; i < authors.Count; i++)
            db.Comments.Add(new Comment { UserId = authors[i].Id, ProductId = graph.Product.Id, OrderItemId = graph.Line.Id,
                Body = $"Comment {i}", ApprovalStatus = i == 0 ? ApprovalStatus.Approved : ApprovalStatus.Pending });
        await db.SaveChangesAsync();
        var service = new ModerationService(db, TimeProvider.System);
        var first = await service.ListAsync("Pending", 0, default);
        Assert.Equal(ModerationService.PageSize, first.Comments.Count);
        Assert.NotNull(first.NextAfterId);
        Assert.Equal(first.Comments.Select(c => c.Id).Order(), first.Comments.Select(c => c.Id));
        var second = await service.ListAsync("Pending", first.NextAfterId!.Value, default);
        Assert.Single(second.Comments);
        Assert.Null(second.NextAfterId);
        Assert.Single((await service.ListAsync("Approved", 0, default)).Comments);
    }

    [Fact]
    public async Task Approve_MakesTheCommentPublicAndRecordsTheModerator()
    {
        await using var db = NewDb();
        var (graph, manager, commentId) = await SeedPendingAsync(db);
        var reviews = new ReviewService(db, TimeProvider.System);
        Assert.Empty((await reviews.PublicAsync(graph.Product.Id, 0, default)).Comments);

        var result = await new ModerationService(db, TimeProvider.System)
            .DecideAsync(manager.Id, commentId, new("Approve", "  Looks fine  "), default);

        Assert.True(result.Changed);
        Assert.Equal("Approved", result.Comment.Status);
        Assert.Equal("Looks fine", result.Comment.ModerationNote);
        Assert.NotNull(result.Comment.ModeratedAt);
        var stored = await db.Comments.AsNoTracking().SingleAsync();
        Assert.Equal(manager.Id, stored.ModeratedByUserId);
        var pub = await reviews.PublicAsync(graph.Product.Id, 0, default);
        Assert.Equal("Works well", Assert.Single(pub.Comments).Body);
        Assert.Equal("Approved", (await reviews.OwnAsync(graph.Buyer.Id, graph.Product.Id, default)).Comment!.Status);
        Assert.Empty((await new ModerationService(db, TimeProvider.System).ListAsync("Pending", 0, default)).Comments);
    }

    [Fact]
    public async Task Reject_KeepsTheCommentOutOfThePublicListAndTheRatingPublic()
    {
        await using var db = NewDb();
        var (graph, manager, commentId) = await SeedPendingAsync(db);
        var result = await new ModerationService(db, TimeProvider.System)
            .DecideAsync(manager.Id, commentId, new("Reject", null), default);
        Assert.Equal("Rejected", result.Comment.Status);
        Assert.Null(result.Comment.ModerationNote);
        var reviews = new ReviewService(db, TimeProvider.System);
        var pub = await reviews.PublicAsync(graph.Product.Id, 0, default);
        Assert.Empty(pub.Comments);
        Assert.Equal(4d, pub.AverageRating); Assert.Equal(1, pub.RatingCount);
        Assert.Equal("Rejected", (await reviews.OwnAsync(graph.Buyer.Id, graph.Product.Id, default)).Comment!.Status);
        Assert.Single(await db.Ratings.ToListAsync());
    }

    [Fact]
    public async Task Decide_RepeatingTheSameDecisionIsASafeRetry()
    {
        await using var db = NewDb();
        var (_, manager, commentId) = await SeedPendingAsync(db);
        var service = new ModerationService(db, TimeProvider.System);
        var first = await service.DecideAsync(manager.Id, commentId, new("Approve", "First"), default);
        var again = await service.DecideAsync(manager.Id, commentId, new("Approve", "Changed"), default);
        Assert.True(first.Changed); Assert.False(again.Changed);
        Assert.Equal("First", again.Comment.ModerationNote);
        Assert.Equal(first.Comment.ModeratedAt, again.Comment.ModeratedAt);
    }

    [Theory]
    [InlineData("Approve", "Reject")]
    [InlineData("Reject", "Approve")]
    public async Task Decide_ADifferentDecisionOnADecidedCommentIsAConflict(string first, string second)
    {
        await using var db = NewDb();
        var (_, manager, commentId) = await SeedPendingAsync(db);
        var service = new ModerationService(db, TimeProvider.System);
        await service.DecideAsync(manager.Id, commentId, new(first, null), default);
        Assert.Equal(409, (await Assert.ThrowsAsync<PurchaseException>(() =>
            service.DecideAsync(manager.Id, commentId, new(second, null), default))).Status);
        Assert.Equal(first == "Approve" ? ApprovalStatus.Approved : ApprovalStatus.Rejected,
            (await db.Comments.AsNoTracking().SingleAsync()).ApprovalStatus);
    }

    [Theory]
    [InlineData(UserRole.Customer)]
    [InlineData(UserRole.SalesManager)]
    [InlineData(UserRole.SupportAgent)]
    public async Task Decide_OnlyAProductManagerMayModerate(UserRole role)
    {
        await using var db = NewDb();
        var (_, _, commentId) = await SeedPendingAsync(db);
        var other = await AddManagerAsync(db, role);
        Assert.Equal(403, (await Assert.ThrowsAsync<PurchaseException>(() =>
            new ModerationService(db, TimeProvider.System).DecideAsync(other.Id, commentId, new("Approve", null), default))).Status);
        Assert.Equal(ApprovalStatus.Pending, (await db.Comments.AsNoTracking().SingleAsync()).ApprovalStatus);
    }

    [Fact]
    public async Task Decide_RejectsAnInactiveOrUnknownModeratorAndAnUnknownComment()
    {
        await using var db = NewDb();
        var (_, manager, commentId) = await SeedPendingAsync(db);
        var service = new ModerationService(db, TimeProvider.System);
        Assert.Equal(404, (await Assert.ThrowsAsync<PurchaseException>(() =>
            service.DecideAsync(manager.Id, commentId + 99, new("Approve", null), default))).Status);
        Assert.Equal(404, (await Assert.ThrowsAsync<PurchaseException>(() =>
            service.DecideAsync(manager.Id, 0, new("Approve", null), default))).Status);
        Assert.Equal(400, (await Assert.ThrowsAsync<PurchaseException>(() =>
            service.DecideAsync(manager.Id, commentId, new("Maybe", null), default))).Status);
        Assert.Equal(401, (await Assert.ThrowsAsync<PurchaseException>(() =>
            service.DecideAsync(manager.Id + 999, commentId, new("Approve", null), default))).Status);
        manager.IsActive = false; await db.SaveChangesAsync();
        Assert.Equal(401, (await Assert.ThrowsAsync<PurchaseException>(() =>
            service.DecideAsync(manager.Id, commentId, new("Approve", null), default))).Status);
        Assert.Equal(ApprovalStatus.Pending, (await db.Comments.AsNoTracking().SingleAsync()).ApprovalStatus);
    }
}
