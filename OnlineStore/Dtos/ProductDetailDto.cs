using System.Linq.Expressions;
using OnlineStore.Models;
using OnlineStore.Catalog;

namespace OnlineStore.Dtos;

// What the product detail API returns (section 5.1 of docs/api-contract-catalog.md).
// SKU, serial number, cost and isActive are internal and are not returned to customers.
public record ProductDetailDto(
    long Id,
    string Name,
    string Model,
    string? Description,
    decimal Price,
    int StockQuantity,
    bool InStock,
    string? ImageUrl,
    long CategoryId,
    string CategoryName,
    string WarrantyStatus,
    string DistributorInformation,
    string? BrandName = null,
    IReadOnlyList<ProductImageDto>? Images = null,
    decimal? ListPrice = null,
    decimal? DiscountRate = null)
{
    // An Expression so EF Core translates it into SQL and selects only these columns.
    public static readonly Expression<Func<Product, ProductDetailDto>> FromProduct =
        product => new ProductDetailDto(
            product.Id,
            product.Name,
            product.Model,
            product.Description,
            product.Price,
            product.StockQuantity,
            product.StockQuantity > 0,
            product.Images.Where(image => image.IsPrimary).OrderBy(image => image.SortOrder).ThenBy(image => image.Id)
                .Select(image => image.StorageKey).FirstOrDefault(),
            product.CategoryId,
            product.Category.Name,
            product.WarrantyStatus,
            product.Distributor.ContactInfo,
            product.Brand != null && product.Brand.IsActive ? product.Brand.Name : null,
            product.Images.OrderBy(image => image.SortOrder).ThenBy(image => image.Id)
                .Select(image => new ProductImageDto(image.StorageKey, image.AltText)).ToList(), product.Price, 0m);

    public static readonly Expression<Func<PricedProduct, ProductDetailDto>> FromPricedProduct = priced =>
        new ProductDetailDto(priced.Product.Id, priced.Product.Name, priced.Product.Model, priced.Product.Description,
            priced.Price, priced.Product.StockQuantity, priced.Product.StockQuantity > 0,
            priced.Product.Images.Where(i => i.IsPrimary).OrderBy(i => i.SortOrder).ThenBy(i => i.Id).Select(i => i.StorageKey).FirstOrDefault(),
            priced.Product.CategoryId, priced.Product.Category.Name, priced.Product.WarrantyStatus,
            priced.Product.Distributor.ContactInfo,
            priced.Product.Brand != null && priced.Product.Brand.IsActive ? priced.Product.Brand.Name : null,
            priced.Product.Images.OrderBy(i => i.SortOrder).ThenBy(i => i.Id)
                .Select(i => new ProductImageDto(i.StorageKey, i.AltText)).ToList(), priced.ListPrice, priced.DiscountRate);
}

public sealed record ProductImageDto(string Url, string? AltText);
