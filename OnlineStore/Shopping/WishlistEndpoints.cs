using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using OnlineStore.Accounts;
using OnlineStore.Data;
using OnlineStore.Dtos;
using OnlineStore.Catalog;

namespace OnlineStore.Shopping;

public static class WishlistEndpoints
{
    public static void MapWishlistEndpoints(this WebApplication app)
    {
        var customer = app.MapCustomerApi()
            .WithMetadata(PrivateResponseMetadata.Instance)
            .AddEndpointFilter<WishlistErrorFilter>();

        customer.MapGet("/wishlist", async (ClaimsPrincipal principal, StoreDbContext db, HttpContext context, CancellationToken ct) =>
        {
            if (!TryOwner(principal, out var owner)) return Results.Unauthorized();
            var priced = ProductPricing.Query(db, ProductPricing.UtcNow(context.RequestServices))
                .Where(p => p.Product.IsActive && p.Product.Category.IsActive);
            var items = await (from item in db.WishlistItems.AsNoTracking()
                               join product in priced on item.ProductId equals product.Product.Id
                               where item.UserId == owner
                               orderby item.AddedAt descending, item.ProductId
                               select new WishlistProductDto(item.ProductId, item.AddedAt,
                                   new ProductListItemDto(product.Product.Id, product.Product.Name, product.Price,
                                       product.Product.StockQuantity, product.Product.StockQuantity > 0,
                                       product.Product.Images.Where(i => i.IsPrimary).OrderBy(i => i.SortOrder).ThenBy(i => i.Id).Select(i => i.StorageKey).FirstOrDefault(),
                                       product.Product.CategoryId, product.Product.Category.Name, product.Product.Description,
                                       product.Product.Brand != null && product.Product.Brand.IsActive ? product.Product.Brand.Name : null,
                                       product.ListPrice, product.DiscountRate, null, 0))).ToListAsync(ct);
            return Results.Ok(items);
        });

        customer.MapPost("/wishlist/{productId:long}", async (
            long productId, ClaimsPrincipal principal, StoreDbContext db, CancellationToken ct) =>
        {
            if (!TryOwner(principal, out var owner)) return Results.Unauthorized();
            if (productId <= 0) return Results.NotFound();
            var productExists = await db.Products.AnyAsync(
                product => product.Id == productId && product.IsActive && product.Category.IsActive, ct);
            if (!productExists) return Results.NotFound();

            var exists = await db.WishlistItems.AnyAsync(item => item.UserId == owner && item.ProductId == productId, ct);
            if (!exists)
            {
                db.WishlistItems.Add(new Models.WishlistItem { UserId = owner, ProductId = productId });
                await db.SaveChangesAsync(ct);
            }
            return Results.NoContent();
        }).AddEndpointFilter<CsrfFilter>();

        customer.MapDelete("/wishlist/{productId:long}", async (
            long productId, ClaimsPrincipal principal, StoreDbContext db, CancellationToken ct) =>
        {
            if (!TryOwner(principal, out var owner)) return Results.Unauthorized();
            var item = await db.WishlistItems.SingleOrDefaultAsync(
                row => row.UserId == owner && row.ProductId == productId, ct);
            if (item is not null)
            {
                db.WishlistItems.Remove(item);
                await db.SaveChangesAsync(ct);
            }
            return Results.NoContent();
        }).AddEndpointFilter<CsrfFilter>();
    }

    private static bool TryOwner(ClaimsPrincipal principal, out long owner) =>
        long.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out owner) && owner > 0;
}

public sealed record WishlistProductDto(long ProductId, DateTime AddedAt, ProductListItemDto Product);

public sealed class WishlistErrorFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try { return await next(context); }
        catch (Exception exception) when (DatabaseErrors.IsUnavailable(exception))
        {
            return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "The wishlist service is temporarily unavailable.",
                detail: "Please try again shortly.");
        }
    }
}
