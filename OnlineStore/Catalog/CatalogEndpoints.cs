using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using OnlineStore.Data;

namespace OnlineStore.Catalog;

public static class CatalogEndpoints
{
    public static void MapCatalogEndpoints(this WebApplication app)
    {
        app.MapGet("/api/catalog", async (
            StoreDbContext db, ILoggerFactory loggerFactory, CancellationToken cancellationToken) =>
        {
            try
            {
                var categories = await db.Categories
                    .AsNoTracking()
                    .Where(category => category.IsActive)
                    .OrderBy(category => category.Name)
                    .ThenBy(category => category.Id)
                    .Select(category => new CategoryListing(
                        category.Id,
                        category.Name,
                        category.Description,
                        category.Products
                            .Where(product => product.IsActive && product.Category.IsActive)
                            .OrderBy(product => product.Name)
                            .ThenBy(product => product.Id)
                            .Select(product => new ProductListing(
                                product.Id, product.Name, product.Description,
                                product.Price, product.StockQuantity, product.Images.Where(i => i.IsPrimary).Select(i => i.StorageKey).FirstOrDefault()))
                            .ToList()))
                    .ToListAsync(cancellationToken);

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
    long Id, string Name, string? Description, decimal Price, int StockQuantity, string? ImageUrl);
