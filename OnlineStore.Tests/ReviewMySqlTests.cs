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
