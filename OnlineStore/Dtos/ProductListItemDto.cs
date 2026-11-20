using System.Linq.Expressions;
using OnlineStore.Models;
using OnlineStore.Catalog;

namespace OnlineStore.Dtos;

// What the product list API returns for one product.
public record ProductListItemDto(
    long Id,
    string Name,
    decimal Price,
    int StockQuantity,
    bool InStock,
    string? ImageUrl,
    long CategoryId,
    string CategoryName,
    string? Description = null,
    string? BrandName = null,
    decimal? ListPrice = null,
    decimal? DiscountRate = null)
{
    // Kept as an Expression so EF Core can translate it into SQL
    // (only the needed columns are selected) and tests can still Compile() it.
    public static readonly Expression<Func<Product, ProductListItemDto>> FromProduct =
        product => new ProductListItemDto(
            product.Id,
            product.Name,
            product.Price,
            product.StockQuantity,
            product.StockQuantity > 0,
            product.Images.Where(image => image.IsPrimary).Select(image => image.StorageKey).FirstOrDefault(),
            product.CategoryId,
            product.Category.Name,
            product.Description,
            product.Brand != null && product.Brand.IsActive ? product.Brand.Name : null, product.Price, 0m);

    public static readonly Expression<Func<PricedProduct, ProductListItemDto>> FromPricedProduct = priced =>
        new ProductListItemDto(priced.Product.Id, priced.Product.Name, priced.Price, priced.Product.StockQuantity,
            priced.Product.StockQuantity > 0,
            priced.Product.Images.Where(i => i.IsPrimary).OrderBy(i => i.SortOrder).ThenBy(i => i.Id).Select(i => i.StorageKey).FirstOrDefault(),
            priced.Product.CategoryId, priced.Product.Category.Name, priced.Product.Description,
            priced.Product.Brand != null && priced.Product.Brand.IsActive ? priced.Product.Brand.Name : null,
            priced.ListPrice, priced.DiscountRate);
}
