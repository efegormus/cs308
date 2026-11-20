using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using OnlineStore.Data;
using OnlineStore.Dtos;

namespace OnlineStore.Catalog;

public static class CatalogEndpoints
{
    public static void MapCatalogEndpoints(this WebApplication app)
    {
        app.MapGet("/api/catalog", async (
            StoreDbContext db, ILoggerFactory loggerFactory, HttpContext context, CancellationToken cancellationToken) =>
        {
            try
            {
                var categories = await db.Categories.AsNoTracking().Where(c => c.IsActive)
                    .OrderBy(c => c.Name).ThenBy(c => c.Id)
                    .Select(c => new CategoryListing(c.Id, c.Name, c.Description, new List<ProductListing>()))
                    .ToListAsync(cancellationToken);
                var products = await ProductPricing.Query(db, ProductPricing.UtcNow(context.RequestServices))
                    .Where(p => p.Product.IsActive && p.Product.Category.IsActive)
                    .Select(ProductListItemDto.FromPricedProduct).ToListAsync(cancellationToken);
                var grouped = products.OrderBy(p => p.Name).ThenBy(p => p.Id).ToLookup(p => p.CategoryId);
                foreach (var category in categories)
                    category.Products.AddRange(grouped[category.Id].Select(p => new ProductListing(p.Id, p.Name,
                        p.Description, p.Price, p.StockQuantity, p.ImageUrl, p.ListPrice, p.DiscountRate)));

                return Results.Ok(categories);
            }
            catch (Exception exception) when (exception is DbException
                || exception is InvalidOperationException { InnerException: DbException })
            {
                loggerFactory.CreateLogger("Catalog").LogError("The catalog database query failed.");
                return Results.Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "The catalog is temporarily unavailable.",
                    detail: "Please try again shortly.");
            }
        }).AllowAnonymous();
    }
}

public sealed record CategoryListing(
    long Id, string Name, string? Description, List<ProductListing> Products);

public sealed record ProductListing(
    long Id, string Name, string? Description, decimal Price, int StockQuantity, string? ImageUrl, decimal? ListPrice = null, decimal? DiscountRate = null);
