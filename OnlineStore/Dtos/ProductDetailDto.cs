using System.Linq.Expressions;
using OnlineStore.Models;

namespace OnlineStore.Dtos;

// What the product detail API returns (section 5.1 of docs/api-contract-catalog.md).
// SKU, serial number, cost and isActive are internal and are not returned to customers.
public record ProductDetailDto(
    int Id,
    string Name,
    string Model,
    string? Description,
    decimal Price,
    int StockQuantity,
    bool InStock,
    string? ImageUrl,
    int CategoryId,
    string CategoryName,
    string WarrantyStatus,
    string DistributorInformation)
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
            product.ImageUrl,
            product.CategoryId,
            product.Category.Name,
            product.WarrantyStatus,
            product.DistributorInformation);
}
