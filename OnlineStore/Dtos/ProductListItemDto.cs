using System.Linq.Expressions;
using OnlineStore.Models;

namespace OnlineStore.Dtos;

// What the product list API returns for one product.
public record ProductListItemDto(
    int Id,
    string Name,
    decimal Price,
    int StockQuantity,
    bool InStock,
    string? ImageUrl,
    int CategoryId,
    string CategoryName)
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
            product.ImageUrl,
            product.CategoryId,
            product.Category.Name);
}
