using System.Security.Claims;
using OnlineStore.Accounts;
using OnlineStore.Purchasing;

namespace OnlineStore.Pricing;

// SCRUM-155: attached to the /api/sales group, so only the SalesManager role reaches these routes.
public static class PricingEndpoints
{
    public static void MapPricingEndpoints(this WebApplication app)
    {
        var sales = app.MapSalesManagerApi().AddEndpointFilter<PurchaseErrorFilter>();

        sales.MapGet("/products", async (long? afterId, ClaimsPrincipal user, PricingService service,
            CancellationToken ct) => Results.Ok(await service.ListProductsAsync(Owner(user), afterId ?? 0, ct)));

        sales.MapPut("/products/{id:long}/price", async (long id, PriceChange input, ClaimsPrincipal user,
            PricingService service, CancellationToken ct) =>
        {
            var errors = input.Validate();
            if (errors.Count != 0) return Results.ValidationProblem(errors);
            return Results.Ok(await service.SetPriceAsync(Owner(user), id, input, ct));
        }).AddEndpointFilter<CsrfFilter>();

        sales.MapGet("/discounts", async (long? beforeId, ClaimsPrincipal user, PricingService service,
            CancellationToken ct) => Results.Ok(await service.ListDiscountsAsync(Owner(user), beforeId ?? 0, ct)));

        sales.MapPost("/discounts", async (DiscountRequest input, ClaimsPrincipal user, PricingService service,
            CancellationToken ct) =>
        {
            var errors = input.Validate();
            if (errors.Count != 0) return Results.ValidationProblem(errors);
            return Results.Json(await service.CreateDiscountAsync(Owner(user), input, ct), statusCode: 201);
        }).AddEndpointFilter<CsrfFilter>();

        sales.MapPost("/discounts/{id:long}/cancel", async (long id, ClaimsPrincipal user, PricingService service,
            CancellationToken ct) => Results.Ok(await service.CancelDiscountAsync(Owner(user), id, ct)))
            .AddEndpointFilter<CsrfFilter>();
    }

    private static long Owner(ClaimsPrincipal user) => long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
