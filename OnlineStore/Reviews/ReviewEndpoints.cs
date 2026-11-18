using System.Security.Claims;
using OnlineStore.Accounts;
using OnlineStore.Purchasing;

namespace OnlineStore.Reviews;

public static class ReviewEndpoints
{
    public static void MapReviewEndpoints(this WebApplication app)
    {
        app.MapGet("/api/products/{productId:long}/reviews", async (long productId, long? afterId,
            ReviewService reviews, CancellationToken ct) => Results.Ok(await reviews.PublicAsync(productId, afterId ?? 0, ct)))
            .AllowAnonymous().AddEndpointFilter<PurchaseErrorFilter>();

        var customer = app.MapCustomerApi().AddEndpointFilter<PurchaseErrorFilter>();
        customer.MapGet("/products/{productId:long}/review", async (long productId, ClaimsPrincipal user,
            ReviewService reviews, CancellationToken ct) => Results.Ok(await reviews.OwnAsync(Owner(user), productId, ct)));
        customer.MapPost("/products/{productId:long}/review", async (long productId, ReviewSubmission input,
            ClaimsPrincipal user, ReviewService reviews, CancellationToken ct) =>
        {
            var errors = input.Validate();
            if (errors.Count != 0) return Results.ValidationProblem(errors);
            var result = await reviews.SubmitAsync(Owner(user), productId, input, ct);
            return Results.Json(result.Review, statusCode: result.Created ? 201 : 200);
        }).AddEndpointFilter<CsrfFilter>();
    }

    private static long Owner(ClaimsPrincipal user) => long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
