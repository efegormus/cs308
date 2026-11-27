using System.Security.Claims;
using OnlineStore.Accounts;
using OnlineStore.Purchasing;

namespace OnlineStore.Catalog;

// SCRUM-77: attached to the /api/product-management group, so only the ProductManager role reaches these routes.
// Every change needs the CSRF token. The contract is in docs/catalog-management.md.
public static class CatalogManagementEndpoints
{
    public static void MapCatalogManagementEndpoints(this WebApplication app)
    {
        var manager = app.MapProductManagerApi().AddEndpointFilter<PurchaseErrorFilter>();

        manager.MapGet("/categories", async (ClaimsPrincipal user, CatalogManagementService service,
            CancellationToken ct) => Results.Ok(await service.ListCategoriesAsync(Owner(user), ct)));
        manager.MapPost("/categories", async (CategoryCreate input, ClaimsPrincipal user,
            CatalogManagementService service, CancellationToken ct) =>
        {
            var errors = input.Validate();
            if (errors.Count != 0) return Results.ValidationProblem(errors);
            return Results.Json(await service.CreateCategoryAsync(Owner(user), input, ct), statusCode: 201);
        }).AddEndpointFilter<CsrfFilter>();
        manager.MapPut("/categories/{id:long}", async (long id, CategoryEdit input, ClaimsPrincipal user,
            CatalogManagementService service, CancellationToken ct) =>
        {
            var errors = input.Validate();
            if (errors.Count != 0) return Results.ValidationProblem(errors);
            return Results.Ok(await service.EditCategoryAsync(Owner(user), id, input, ct));
        }).AddEndpointFilter<CsrfFilter>();
        manager.MapDelete("/categories/{id:long}", async (long id, ClaimsPrincipal user,
            CatalogManagementService service, CancellationToken ct) =>
        {
            await service.DeleteCategoryAsync(Owner(user), id, ct);
            return Results.NoContent();
        }).AddEndpointFilter<CsrfFilter>();

        manager.MapGet("/products", async (long? afterId, ClaimsPrincipal user, CatalogManagementService service,
            CancellationToken ct) => Results.Ok(await service.ListProductsAsync(Owner(user), afterId ?? 0, ct)));
        manager.MapPost("/products", async (ProductCreate input, ClaimsPrincipal user,
            CatalogManagementService service, CancellationToken ct) =>
        {
            var errors = input.Validate();
            if (errors.Count != 0) return Results.ValidationProblem(errors);
            return Results.Json(await service.CreateProductAsync(Owner(user), input, ct), statusCode: 201);
        }).AddEndpointFilter<CsrfFilter>();
        manager.MapPut("/products/{id:long}", async (long id, ProductEdit input, ClaimsPrincipal user,
            CatalogManagementService service, CancellationToken ct) =>
        {
            var errors = input.Validate();
            if (errors.Count != 0) return Results.ValidationProblem(errors);
            return Results.Ok(await service.EditProductAsync(Owner(user), id, input, ct));
        }).AddEndpointFilter<CsrfFilter>();
        manager.MapDelete("/products/{id:long}", async (long id, ClaimsPrincipal user,
            CatalogManagementService service, CancellationToken ct) =>
            Results.Ok(await service.RemoveProductAsync(Owner(user), id, ct))).AddEndpointFilter<CsrfFilter>();
    }

    private static long Owner(ClaimsPrincipal user) => long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
