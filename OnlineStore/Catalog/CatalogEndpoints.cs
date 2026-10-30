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
                    .OrderBy(category => category.Name)
                    .ThenBy(category => category.Id)
                    .Select(category => new CategoryListing(
                        category.Id,
                        category.Name,
                        category.Description,
                        category.Products
                            .Where(product => product.IsActive)
                            .OrderBy(product => product.Name)
                            .ThenBy(product => product.Id)
                            .Select(product => new ProductListing(
                                product.Id, product.Name, product.Description,
                                product.Price, product.StockQuantity, product.ImageUrl))
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
        });
    }
}

public sealed record CategoryListing(
    int Id, string Name, string? Description, List<ProductListing> Products);

public sealed record ProductListing(
    int Id, string Name, string? Description, decimal Price, int StockQuantity, string? ImageUrl);
