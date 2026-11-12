using System.Linq.Expressions;
using OnlineStore.Models;

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
    IReadOnlyList<ProductImageDto>? Images = null)
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
                .Select(image => new ProductImageDto(image.StorageKey, image.AltText)).ToList());
}

public sealed record ProductImageDto(string Url, string? AltText);
