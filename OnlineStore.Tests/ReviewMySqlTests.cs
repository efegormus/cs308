using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using OnlineStore.Data;
using OnlineStore.Models;
using OnlineStore.Purchasing;
using OnlineStore.Reviews;

namespace OnlineStore.Tests;

[Collection("MySql")]
public class ReviewMySqlTests(MySqlFixture fixture) : IClassFixture<MySqlFixture>
{
    [MySqlFact]
    public async Task MySql_ModerationRequiresCsrfPublishesApprovedOnlyAndDoesNotChangeRatings()
    {
        await fixture.ResetAsync(); ReviewFixture.Graph g;
        await using (var seed = fixture.Create())
        {
            g = await ReviewFixture.SeedAsync(seed); g.Other.Role = UserRole.ProductManager; await seed.SaveChangesAsync();
            await new ReviewService(seed, TimeProvider.System).SubmitAsync(g.Buyer.Id, g.Product.Id, new(4, "Waiting for manager"), default);
        }
        using var app = new ReviewApplication(fixture); using var manager = app.CreateClient(); using var customer = app.CreateClient(); using var guest = app.CreateClient();
        await Login(manager, g.Other.Email); await Login(customer, g.Buyer.Email);
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.GetAsync("/api/product-management/comments")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/product-management/comments")).StatusCode);
        var page = (await manager.GetFromJsonAsync<ModerationPage>("/api/product-management/comments"))!;
        var comment = Assert.Single(page.Items); Assert.Equal(4, comment.Rating); Assert.Equal(g.Buyer.Name, comment.CustomerName);
        var path = $"/api/product-management/comments/{comment.Id}/moderation";
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(customer, path, new ModerationRequest("Approved", comment.Version))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await manager.PostAsJsonAsync(path, new ModerationRequest("Approved", comment.Version))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(manager, path, new ModerationRequest("Approved", "0"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(manager, path, new { status = "Approved", version = comment.Version, userId = g.Buyer.Id })).StatusCode);
        Assert.Empty((await guest.GetFromJsonAsync<ProductReviews>($"/api/products/{g.Product.Id}/reviews"))!.Comments);
        var approved = await Post(manager, path, new ModerationRequest("Approved", comment.Version)); Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var decision = (await approved.Content.ReadFromJsonAsync<ModerationResult>())!;
        Assert.NotEqual(comment.Version, decision.Version);
        Assert.Equal(HttpStatusCode.OK, (await Post(manager, path, new ModerationRequest("Approved", comment.Version))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(manager, path, new ModerationRequest("Rejected", decision.Version))).StatusCode);
        var published = (await guest.GetFromJsonAsync<ProductReviews>($"/api/products/{g.Product.Id}/reviews"))!;
        Assert.Equal("Waiting for manager", Assert.Single(published.Comments).Body); Assert.Equal(4d, published.AverageRating);
        Assert.Empty((await manager.GetFromJsonAsync<ModerationPage>("/api/product-management/comments"))!.Items);
        Assert.Equal("Approved", (await customer.GetFromJsonAsync<CustomerReview>($"/api/customer/products/{g.Product.Id}/review"))!.Comment!.Status);
    }

    [MySqlFact]
    public async Task MySql_ConcurrentOppositeModerationHasOneWinnerAndRejectedTextStaysPrivate()
    {
        await fixture.ResetAsync(); ReviewFixture.Graph g; ModerationComment comment;
        await using (var seed = fixture.Create())
        {
            g = await ReviewFixture.SeedAsync(seed); g.Other.Role = UserRole.ProductManager; await seed.SaveChangesAsync();
            var service = new ReviewService(seed, TimeProvider.System);
            await service.SubmitAsync(g.Buyer.Id, g.Product.Id, new(3, "Private until approved"), default);
            comment = Assert.Single((await service.PendingAsync(0, default)).Items);
        }
        async Task<int> Decide(string status)
        {
            await using var db = fixture.Create();
            try { await new ReviewService(db, TimeProvider.System).ModerateAsync(g.Other.Id, comment.Id, new(status, comment.Version), default); return 200; }
            catch (PurchaseException error) { return error.Status; }
        }
        Assert.Equal([200,409], (await Task.WhenAll(Decide("Approved"), Decide("Rejected"))).Order());
        await using var check = fixture.Create();
        var stored = await check.Comments.SingleAsync(); Assert.Equal(g.Other.Id, stored.ModeratedByUserId); Assert.NotNull(stored.ModeratedAt);
        var publicPage = await new ReviewService(check, TimeProvider.System).PublicAsync(g.Product.Id, 0, default);
        Assert.Equal(stored.ApprovalStatus == ApprovalStatus.Approved ? 1 : 0, publicPage.Comments.Count);
        Assert.Equal(3d, publicPage.AverageRating);
    }

    [MySqlFact]
    public async Task MySql_SalesAndSupportCannotModerateAndRejectedCommentsNeverAppearPublicly()
    {
        await fixture.ResetAsync(); ReviewFixture.Graph g;
        await using (var seed = fixture.Create())
        {
            g = await ReviewFixture.SeedAsync(seed);
            await new ReviewService(seed, TimeProvider.System).SubmitAsync(g.Buyer.Id, g.Product.Id, new(null, "Rejected body"), default);
        }
        using var app = new ReviewApplication(fixture);
        foreach (var role in new[] { UserRole.SalesManager, UserRole.SupportAgent })
        {
            await using (var db = fixture.Create()) { var user = await db.Users.SingleAsync(u => u.Id == g.Other.Id); user.Role = role; user.SecurityStamp = Guid.NewGuid().ToString("N"); await db.SaveChangesAsync(); }
            using var staff = app.CreateClient(); await Login(staff, g.Other.Email);
            Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync("/api/product-management/comments")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await Post(staff, "/api/product-management/comments/1/moderation", new ModerationRequest("Approved", "1"))).StatusCode);
        }
        await using var check = fixture.Create(); var manager = await check.Users.SingleAsync(u => u.Id == g.Other.Id); manager.Role = UserRole.ProductManager; await check.SaveChangesAsync();
        var service = new ReviewService(check, TimeProvider.System); var pending = Assert.Single((await service.PendingAsync(0, default)).Items);
        await service.ModerateAsync(manager.Id, pending.Id, new("Rejected", pending.Version), default);
        Assert.Empty((await service.PublicAsync(g.Product.Id, 0, default)).Comments);
        Assert.Equal("Rejected", (await service.OwnAsync(g.Buyer.Id, g.Product.Id, default)).Comment!.Status);
    }

    [MySqlFact]
    public async Task MySql_ConcurrentIdenticalReviewsCommitOneRatingAndOnePendingComment()
    {
        await fixture.ResetAsync();
        ReviewFixture.Graph g;
        await using (var seed = fixture.Create()) g = await ReviewFixture.SeedAsync(seed);
        async Task<ReviewResult> Submit()
        {
            await using var db = fixture.Create();
            return await new ReviewService(db, TimeProvider.System).SubmitAsync(g.Buyer.Id, g.Product.Id, new(5, "Received"), default);
        }
        var results = await Task.WhenAll(Submit(), Submit());
        Assert.Single(results, r => r.Created);
        await using var check = fixture.Create();
        Assert.Single(await check.Ratings.ToListAsync()); Assert.Single(await check.Comments.ToListAsync());
        Assert.Equal(ApprovalStatus.Pending, (await check.Comments.SingleAsync()).ApprovalStatus);
        var page = await new ReviewService(check, TimeProvider.System).PublicAsync(g.Product.Id, 0, default);
        Assert.Equal(5d, page.AverageRating); Assert.Empty(page.Comments);
    }

    [MySqlFact]
    public async Task MySql_ConflictingConcurrentRatingsHaveOneWinnerAndNoPartialComments()
    {
        await fixture.ResetAsync(); ReviewFixture.Graph g;
        await using (var seed = fixture.Create()) g = await ReviewFixture.SeedAsync(seed);
        async Task<int> Submit(int stars)
        {
            await using var db = fixture.Create();
            try { await new ReviewService(db, TimeProvider.System).SubmitAsync(g.Buyer.Id, g.Product.Id, new(stars, "Review " + stars), default); return 201; }
            catch (PurchaseException error) { return error.Status; }
        }
        Assert.Equal([201, 409], (await Task.WhenAll(Submit(1), Submit(5))).Order().ToArray());
        await using var check = fixture.Create();
        var rating = await check.Ratings.SingleAsync(); var comment = await check.Comments.SingleAsync();
        Assert.Equal("Review " + rating.Stars, comment.Body);
    }

    [MySqlFact]
    public async Task MySql_ReviewHttpEnforcesSessionCsrfOwnerValidationAndModerationVisibility()
    {
        await fixture.ResetAsync(); ReviewFixture.Graph g;
        await using (var seed = fixture.Create()) g = await ReviewFixture.SeedAsync(seed);
        using var app = new ReviewApplication(fixture);
        using var guest = app.CreateClient(); using var buyer = app.CreateClient(); using var other = app.CreateClient();
        var path = $"/api/customer/products/{g.Product.Id}/review";
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.PostAsJsonAsync(path, new { rating = 5 })).StatusCode);
        await Login(buyer, g.Buyer.Email); await Login(other, g.Other.Email);
        Assert.Equal(HttpStatusCode.BadRequest, (await buyer.PostAsJsonAsync(path, new { rating = 5 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(other, path, new { rating = 5 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(buyer, path, new { rating = 6 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(buyer, path, new { rating = 5, userId = g.Other.Id })).StatusCode);
        var saved = await Post(buyer, path, new { rating = 4, comment = "<script>text only</script>" });
        Assert.Equal(HttpStatusCode.Created, saved.StatusCode);
        var own = (await saved.Content.ReadFromJsonAsync<CustomerReview>())!;
        Assert.Equal("Pending", own.Comment!.Status);
        var publicPath = $"/api/products/{g.Product.Id}/reviews";
        var response = await guest.GetAsync(publicPath);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var page = (await response.Content.ReadFromJsonAsync<ProductReviews>())!;
        Assert.Equal(4d, page.AverageRating); Assert.Equal(1, page.RatingCount); Assert.Empty(page.Comments);
        Assert.DoesNotContain("script", await response.Content.ReadAsStringAsync());
        Assert.Null((await other.GetFromJsonAsync<CustomerReview>(path))!.Comment);
        Assert.Equal(HttpStatusCode.OK, (await Post(buyer, path, new { rating = 4, comment = "<script>text only</script>" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(buyer, path, new { rating = 5 })).StatusCode);

        // SCRUM-81 owns the moderator API. Verify its persisted decision is respected by this public contract.
        await using (var db = fixture.Create())
        {
            var comment = await db.Comments.SingleAsync(); comment.ApprovalStatus = ApprovalStatus.Approved;
            comment.ModeratedByUserId = g.Other.Id; comment.ModeratedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        page = (await guest.GetFromJsonAsync<ProductReviews>(publicPath))!;
        Assert.Equal("<script>text only</script>", Assert.Single(page.Comments).Body);
        var json = JsonSerializer.Serialize(page);
        foreach (var secret in new[] { g.Buyer.Email, g.Buyer.HomeAddress, "PasswordHash", "TaxId", "ModeratedByUserId" })
            Assert.DoesNotContain(secret, json);
    }

    [MySqlFact]
    public async Task MySql_StaffRolesCannotReadOrSubmitCustomerReviews()
    {
        await fixture.ResetAsync(); ReviewFixture.Graph g;
        await using (var seed = fixture.Create()) g = await ReviewFixture.SeedAsync(seed);
        using var app = new ReviewApplication(fixture);
        foreach (var role in new[] { UserRole.ProductManager, UserRole.SalesManager, UserRole.SupportAgent })
        {
            await using (var db = fixture.Create())
            {
                var user = await db.Users.SingleAsync(u => u.Id == g.Other.Id); user.Role = role;
                user.SecurityStamp = Guid.NewGuid().ToString("N"); await db.SaveChangesAsync();
            }
            using var staff = app.CreateClient(); await Login(staff, g.Other.Email);
            var path = $"/api/customer/products/{g.Product.Id}/review";
            Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync(path)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await Post(staff, path, new { rating = 5 })).StatusCode);
        }
    }

    private static async Task Login(HttpClient client, string email) =>
        Assert.Equal(HttpStatusCode.OK, (await Post(client, "/api/auth/login", new { email, password = ReviewFixture.Password })).StatusCode);
    private static async Task<HttpResponseMessage> Post(HttpClient client, string path, object body)
    {
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        return await client.SendAsync(request);
    }
    private sealed class ReviewApplication(MySqlFixture fixture) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("MySql:ServerVersion", "8.4.13");
            builder.UseSetting("ConnectionStrings:DefaultConnection", "Server=localhost;Database=unused;User ID=unused;Password=unused");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<StoreDbContext>(); services.AddScoped(_ => fixture.Create());
            });
        }
    }
}
