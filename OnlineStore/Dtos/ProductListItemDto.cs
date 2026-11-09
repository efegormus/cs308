using System.Linq.Expressions;
using OnlineStore.Models;

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
    string? Description = null)
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
            product.Description);
}
