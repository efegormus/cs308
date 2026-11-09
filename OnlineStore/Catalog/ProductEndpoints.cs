using System.Data.Common;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using OnlineStore.Data;
using OnlineStore.Dtos;
using OnlineStore.Models;

namespace OnlineStore.Catalog;

public static class ProductEndpoints
{
    private const int MaxSearchLength = 100;

    public static void MapProductEndpoints(this WebApplication app)
    {
        // Public (guests can browse).
        // GET /api/products                       -> all active products
        // GET /api/products?categoryId=1          -> only that category
        // GET /api/products?search=pixel          -> name or description contains the text (SCRUM-50)
        // GET /api/products?sort=price&order=desc -> sorted by price or popularity (SCRUM-51)
        // Out-of-stock products are included; the UI blocks adding them to the cart.
        app.MapGet("/api/products", async (
            long? categoryId, string? search, string? sort, string? order,
            StoreDbContext db, ILoggerFactory loggerFactory, CancellationToken cancellationToken) =>
        {
            var term = search?.Trim().Replace("ı", "i").Replace("İ", "i");
            if (term is { Length: > MaxSearchLength })
            {
                return BadRequest($"The search text can be at most {MaxSearchLength} characters.");
            }

            var sortBy = sort?.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(sortBy) && sortBy is not ("price" or "popularity"))
            {
                return BadRequest("The sort value must be 'price' or 'popularity'.");
            }

            var direction = order?.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(direction) && direction is not ("asc" or "desc"))
            {
                return BadRequest("The order value must be 'asc' or 'desc'.");
            }

            try
            {
                var query = db.Products.AsNoTracking().Where(product => product.IsActive);

                if (categoryId is not null)
                {
                    var tree = await db.Categories.AsNoTracking().Select(c => new { c.Id, c.ParentId }).ToListAsync(cancellationToken);
                    var categoryIds = new HashSet<long> { categoryId.Value };
                    while (true)
                    {
                        var children = tree.Where(c => c.ParentId is { } parent && categoryIds.Contains(parent) && !categoryIds.Contains(c.Id)).Select(c => c.Id).ToList();
                        if (children.Count == 0) break;
                        categoryIds.UnionWith(children);
                    }
                    query = query.Where(product => categoryIds.Contains(product.CategoryId));
                }

                if (!string.IsNullOrEmpty(term))
                {
                    // Lower-case both sides so matching ignores case whatever the collation, and
                    // escape LIKE wildcards so "%" or "_" typed by a visitor match literally.
                    var pattern = "%" + term.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_").ToLowerInvariant() + "%";
                    query = query.Where(product =>
                        EF.Functions.Like(product.Name.Replace("ı", "i").Replace("İ", "i").ToLower(), pattern, "\\")
                        || (product.Description != null && EF.Functions.Like(product.Description.Replace("ı", "i").Replace("İ", "i").ToLower(), pattern, "\\")));
                }

                var descending = direction == "desc";
                IOrderedQueryable<Product> ordered;
                if (sortBy == "price")
                {
                    ordered = descending
                        ? query.OrderByDescending(product => product.Price)
                        : query.OrderBy(product => product.Price);
                }
                else if (sortBy == "popularity")
                {
                    // Units sold in orders whose payment is confirmed and that are not cancelled.
                    // Payment and fulfillment are separate enums; refunded orders are not Confirmed.
                    var sold = (Expression<Func<Product, int>>)(product => db.OrderItems
                        .Where(item => item.ProductId == product.Id
                            && item.Order.PaymentStatus == PaymentStatus.Confirmed
                            && item.Order.Status != OrderStatus.Cancelled)
                        .Sum(item => (int?)(item.Quantity - item.RefundedQuantity)) ?? 0);
                    // Popularity is highest first unless the caller asks for ascending.
                    ordered = direction == "asc"
                        ? query.OrderBy(sold)
                        : query.OrderByDescending(sold);
                }
                else
                {
                    ordered = query.OrderBy(product => product.Name);
                }

                // Ties are broken by name, then id, so the order is stable.
                if (sortBy is not null && sortBy != "")
                {
                    ordered = ordered.ThenBy(product => product.Name);
                }
                var products = await ordered
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
        // A non-integer id does not match the {id:long} route, so the framework answers 404.
        app.MapGet("/api/products/{id:long}", async (
            long id, StoreDbContext db, ILoggerFactory loggerFactory, CancellationToken cancellationToken) =>
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

    // Same application/problem+json format as the 503 answer.
    private static IResult BadRequest(string detail) =>
        Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "The request is not valid.", detail: detail);

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
