using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using OnlineStore.Data;
using OnlineStore.Dtos;

namespace OnlineStore.Catalog;

public static class ProductEndpoints
{
    public static void MapProductEndpoints(this WebApplication app)
    {
        // Public (guests can browse).
        // GET /api/products              -> all active products
        // GET /api/products?categoryId=1 -> only that category
        // Out-of-stock products are included; the UI blocks adding them to the cart.
        app.MapGet("/api/products", async (
            int? categoryId, StoreDbContext db, ILoggerFactory loggerFactory, CancellationToken cancellationToken) =>
        {
            try
            {
                var query = db.Products.AsNoTracking().Where(product => product.IsActive);

                if (categoryId is not null)
                {
                    query = query.Where(product => product.CategoryId == categoryId);
                }

                var products = await query
                    .OrderBy(product => product.Name)
                    .ThenBy(product => product.Id)
                    .Select(ProductListItemDto.FromProduct)
                    .ToListAsync(cancellationToken);

                return Results.Ok(products);
            }
            catch (Exception exception) when (IsDatabaseFailure(exception))
            {
                return Unavailable(loggerFactory, "products");
            }
        }).AllowAnonymous();

        // Public: one product for the detail page (SCRUM-45, section 5.1 of the API contract).
        // Unknown or inactive product -> 404. Out-of-stock products are returned (inStock = false).
        // A non-integer id does not match the {id:int} route, so the framework answers 404.
        app.MapGet("/api/products/{id:int}", async (
            int id, StoreDbContext db, ILoggerFactory loggerFactory, CancellationToken cancellationToken) =>
        {
            try
            {
                var product = await db.Products
                    .AsNoTracking()
                    .Where(product => product.Id == id && product.IsActive)
                    .Select(ProductDetailDto.FromProduct)
                    .FirstOrDefaultAsync(cancellationToken);

                return product is null
                    ? Results.Problem(
                        statusCode: StatusCodes.Status404NotFound,
                        title: "Product not found.",
                        detail: "No active product has this id.")
                    : Results.Ok(product);
            }
            catch (Exception exception) when (IsDatabaseFailure(exception))
            {
                return Unavailable(loggerFactory, "product");
            }
        }).AllowAnonymous();

        // Public: lists all categories so the site can group products.
        app.MapGet("/api/categories", async (
            StoreDbContext db, ILoggerFactory loggerFactory, CancellationToken cancellationToken) =>
        {
            try
            {
                var categories = await db.Categories
                    .AsNoTracking()
                    .OrderBy(category => category.Name)
                    .ThenBy(category => category.Id)
                    .Select(category => new CategoryDto(category.Id, category.Name, category.Description))
                    .ToListAsync(cancellationToken);

                return Results.Ok(categories);
            }
            catch (Exception exception) when (IsDatabaseFailure(exception))
            {
                return Unavailable(loggerFactory, "categories");
            }
        }).AllowAnonymous();
    }

    private static bool IsDatabaseFailure(Exception exception) =>
        exception is DbException || exception is InvalidOperationException { InnerException: DbException };

    // Same safe 503 answer as /api/catalog: no connection details are leaked.
    private static IResult Unavailable(ILoggerFactory loggerFactory, string what)
    {
        loggerFactory.CreateLogger("Catalog").LogError("The {What} database query failed.", what);
        return Results.Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "The catalog is temporarily unavailable.",
            detail: "Please try again shortly.");
    }
}
