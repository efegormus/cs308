using Microsoft.EntityFrameworkCore;
using OnlineStore.Data;
using OnlineStore.Models;
using OnlineStore.Purchasing;
using OnlineStore.Reviews;

namespace OnlineStore.Tests;

public class ReviewServiceTests
{
    private static StoreDbContext NewDb() => new(new DbContextOptionsBuilder<StoreDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Theory]
    [InlineData(null, null, false)]
    [InlineData(0, null, false)]
    [InlineData(6, null, false)]
    [InlineData(5, " ", false)]
    [InlineData(null, "Delivered safely", true)]
    [InlineData(1, null, true)]
    [InlineData(5, "Good", true)]
    public void Submission_ValidatesIndependentRatingAndComment(int? rating, string? comment, bool valid) =>
        Assert.Equal(valid, new ReviewSubmission(rating, comment).Validate().Count == 0);

    [Fact]
    public void Submission_BoundsCommentLength() =>
        Assert.Contains("comment", new ReviewSubmission(4, new string('x', 2001)).Validate().Keys);

    [Theory]
    [InlineData(OrderStatus.Processing)]
    [InlineData(OrderStatus.InTransit)]
    [InlineData(OrderStatus.Cancelled)]
    public async Task Submit_RejectsProductsNotYetDelivered(OrderStatus status)
    {
        await using var db = NewDb();
        var g = await ReviewFixture.SeedAsync(db, status);
        var service = new ReviewService(db, TimeProvider.System);
        Assert.False((await service.OwnAsync(g.Buyer.Id, g.Product.Id, default)).Eligible);
        Assert.Equal(403, (await Assert.ThrowsAsync<PurchaseException>(() =>
            service.SubmitAsync(g.Buyer.Id, g.Product.Id, new(5, "Not delivered"), default))).Status);
        Assert.Empty(db.Ratings); Assert.Empty(db.Comments);
    }

    [Fact]
    public async Task Submit_RejectsAnotherCustomersPurchaseAndStaffAccounts()
    {
        await using var db = NewDb();
        var g = await ReviewFixture.SeedAsync(db);
        var service = new ReviewService(db, TimeProvider.System);
        Assert.False((await service.OwnAsync(g.Other.Id, g.Product.Id, default)).Eligible);
        Assert.Equal(403, (await Assert.ThrowsAsync<PurchaseException>(() =>
            service.SubmitAsync(g.Other.Id, g.Product.Id, new(5, null), default))).Status);
        g.Buyer.Role = UserRole.ProductManager; await db.SaveChangesAsync();
        Assert.Equal(403, (await Assert.ThrowsAsync<PurchaseException>(() =>
            service.SubmitAsync(g.Buyer.Id, g.Product.Id, new(5, null), default))).Status);
    }

    [Fact]
    public async Task Submit_ShowsRatingImmediatelyAndKeepsPendingTextPrivate()
    {
        await using var db = NewDb();
        var g = await ReviewFixture.SeedAsync(db);
        var service = new ReviewService(db, TimeProvider.System);
        var result = await service.SubmitAsync(g.Buyer.Id, g.Product.Id, new(4, "  Private until approved  "), default);
        Assert.True(result.Created);
        Assert.Equal("Pending", result.Review.Comment!.Status);
        Assert.Equal("Private until approved", result.Review.Comment.Body);
        Assert.False(result.Review.CanRate); Assert.False(result.Review.CanComment);
        var page = await service.PublicAsync(g.Product.Id, 0, default);
        Assert.Equal(4d, page.AverageRating); Assert.Equal(1, page.RatingCount); Assert.Empty(page.Comments);
        var other = await service.OwnAsync(g.Other.Id, g.Product.Id, default);
        Assert.Null(other.Comment); Assert.Null(other.Rating);
    }

    [Fact]
    public async Task Submit_AllowsCommentAfterRatingAndReplaysWithoutDuplicates()
    {
        await using var db = NewDb();
        var g = await ReviewFixture.SeedAsync(db);
        var service = new ReviewService(db, TimeProvider.System);
        await service.SubmitAsync(g.Buyer.Id, g.Product.Id, new(5, null), default);
        Assert.True((await service.OwnAsync(g.Buyer.Id, g.Product.Id, default)).CanComment);
        await service.SubmitAsync(g.Buyer.Id, g.Product.Id, new(null, "Good"), default);
        Assert.False((await service.SubmitAsync(g.Buyer.Id, g.Product.Id, new(5, "Good"), default)).Created);
        Assert.Single(db.Ratings); Assert.Single(db.Comments);
    }

    [Fact]
    public async Task Submit_ConflictingRetryDoesNotWriteTheOtherComponent()
    {
        await using var db = NewDb();
        var g = await ReviewFixture.SeedAsync(db);
        var service = new ReviewService(db, TimeProvider.System);
        await service.SubmitAsync(g.Buyer.Id, g.Product.Id, new(2, null), default);
        Assert.Equal(409, (await Assert.ThrowsAsync<PurchaseException>(() =>
            service.SubmitAsync(g.Buyer.Id, g.Product.Id, new(5, "Must not be inserted"), default))).Status);
        Assert.Empty(db.Comments); Assert.Equal(2, (await db.Ratings.SingleAsync()).Stars);
    }

    [Theory]
    [InlineData(ApprovalStatus.Pending, false)]
    [InlineData(ApprovalStatus.Rejected, false)]
    [InlineData(ApprovalStatus.Approved, true)]
    public async Task Public_OnlyShowsApprovedComments(ApprovalStatus status, bool visible)
    {
        await using var db = NewDb(); var g = await ReviewFixture.SeedAsync(db);
        db.Comments.Add(new Comment { UserId = g.Buyer.Id, ProductId = g.Product.Id,
            OrderItemId = g.Line.Id, Body = "Visible only after approval", ApprovalStatus = status });
        await db.SaveChangesAsync();
        var page = await new ReviewService(db, TimeProvider.System).PublicAsync(g.Product.Id, 0, default);
        Assert.Equal(visible ? 1 : 0, page.Comments.Count);
        Assert.Null(page.AverageRating); Assert.Equal(0, page.RatingCount);
    }

    [Fact]
    public async Task Public_PaginatesWithoutLosingTheFullRatingAverage()
    {
        await using var db = NewDb(); var g = await ReviewFixture.SeedAsync(db);
        for (var i = 0; i < 23; i++)
        {
            var author = new User { Name = "Reader " + i, Email = $"reader{i}@example.com", PasswordHash = "hash" };
            db.Users.Add(author); await db.SaveChangesAsync();
            db.Comments.Add(new Comment { UserId = author.Id, ProductId = g.Product.Id, OrderItemId = g.Line.Id,
                Body = "Approved " + i, ApprovalStatus = ApprovalStatus.Approved });
        }
        db.Ratings.AddRange(new Rating { UserId = g.Buyer.Id, ProductId = g.Product.Id, OrderItemId = g.Line.Id, Stars = 2 },
            new Rating { UserId = g.Other.Id, ProductId = g.Product.Id, OrderItemId = g.Line.Id, Stars = 5 });
        await db.SaveChangesAsync();
        var service = new ReviewService(db, TimeProvider.System);
        var first = await service.PublicAsync(g.Product.Id, 0, default);
        var second = await service.PublicAsync(g.Product.Id, first.NextAfterId!.Value, default);
        Assert.Equal(20, first.Comments.Count); Assert.Equal(3, second.Comments.Count); Assert.Null(second.NextAfterId);
        Assert.Equal(3.5d, second.AverageRating); Assert.Equal(2, second.RatingCount);
        Assert.Empty(first.Comments.Select(c => c.Id).Intersect(second.Comments.Select(c => c.Id)));
    }

    [Fact]
    public async Task Submit_DoesNotAcceptAnInconsistentDeliveredOrderLine()
    {
        await using var db = NewDb(); var g = await ReviewFixture.SeedAsync(db);
        g.Line.DeliveredQuantity = 0; await db.SaveChangesAsync();
        Assert.Equal(403, (await Assert.ThrowsAsync<PurchaseException>(() => new ReviewService(db, TimeProvider.System)
            .SubmitAsync(g.Buyer.Id, g.Product.Id, new(5, null), default))).Status);
    }

    [Fact]
    public async Task UnknownOrInactiveProductAndNegativeCursorAreRejected()
    {
        await using var db = NewDb(); var g = await ReviewFixture.SeedAsync(db);
        var service = new ReviewService(db, TimeProvider.System);
        Assert.Equal(404, (await Assert.ThrowsAsync<PurchaseException>(() => service.PublicAsync(999, 0, default))).Status);
        Assert.Equal(400, (await Assert.ThrowsAsync<PurchaseException>(() => service.PublicAsync(g.Product.Id, -1, default))).Status);
        g.Product.IsActive = false; await db.SaveChangesAsync();
        Assert.Equal(404, (await Assert.ThrowsAsync<PurchaseException>(() => service.OwnAsync(g.Buyer.Id, g.Product.Id, default))).Status);
    }
}

internal static class ReviewFixture
{
    internal sealed record Graph(User Buyer, User Other, Product Product, OrderItem Line);
    public const string Password = "Review test passphrase 123!";
    internal static async Task<Graph> SeedAsync(StoreDbContext db, OrderStatus status = OrderStatus.Delivered)
    {
        var buyer = new User { Name = "Buyer", Email = "buyer@example.com", PasswordHash = "hash", TaxId = "TAX", HomeAddress = "Private address" };
        var other = new User { Name = "Other", Email = "other@example.com", PasswordHash = "hash", TaxId = "OTHER", HomeAddress = "Other address" };
        var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<User>();
        buyer.PasswordHash = hasher.HashPassword(buyer, Password); other.PasswordHash = hasher.HashPassword(other, Password);
        var product = new Product { Sku = "REVIEW-001", Name = "Review product", Price = 100, StockQuantity = 20,
            Category = new Category { Name = "Review category" } };
        CatalogFixture.CompleteDetails(product);
        db.AddRange(buyer, other, product); await db.SaveChangesAsync();
        var order = new Order { UserId = buyer.Id, User = buyer, Subtotal = 100, GrandTotal = 100 }.WithCustomerSnapshot(buyer);
        var line = OrderItem.Snapshot(product, 1); line.UserId = buyer.Id; line.DeliveryAllocatedQuantity = 1;
        order.OrderItems.Add(line);
        var now = DateTime.UtcNow.AddDays(-2);
        order.ConfirmPayment(now);
        if (status is OrderStatus.InTransit or OrderStatus.Delivered) order.TransitionTo(OrderStatus.InTransit, now.AddHours(1));
        if (status == OrderStatus.Delivered) { order.TransitionTo(OrderStatus.Delivered, now.AddDays(1)); line.DeliveredQuantity = 1; }
        if (status == OrderStatus.Cancelled) order.TransitionTo(OrderStatus.Cancelled, now.AddHours(1));
        db.Orders.Add(order); await db.SaveChangesAsync();
        return new(buyer, other, product, line);
    }
}
