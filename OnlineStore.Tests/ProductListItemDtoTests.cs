using OnlineStore.Dtos;
using OnlineStore.Models;
using Xunit;

namespace OnlineStore.Tests;

public class ProductListItemDtoTests
{
    // FromProduct is an Expression (so EF Core can turn it into SQL);
    // Compile() turns it into a normal function we can call without a database.
    private static readonly Func<Product, ProductListItemDto> Map =
        ProductListItemDto.FromProduct.Compile();

    private static Product CreateProduct(int stock, string? imageUrl = null) => new()
    {
        Id = 7,
        CategoryId = 2,
        Sku = "PHONE-001",
        Name = "Test Phone",
        Price = 999.99m,
        StockQuantity = stock,
        ImageUrl = imageUrl,
        Category = new Category { Id = 2, Name = "Phones" }
    };

    [Theory]
    [InlineData(1, true)]    // boundary: exactly one item left
    [InlineData(25, true)]
    [InlineData(0, false)]   // boundary: sold out
    public void Map_SetsInStockFromStockQuantity(int stock, bool expectedInStock)
    {
        var dto = Map(CreateProduct(stock));

        Assert.Equal(expectedInStock, dto.InStock);
    }

    [Fact]
    public void Map_CopiesBasicProductFields()
    {
        var dto = Map(CreateProduct(stock: 4));

        Assert.Equal(7, dto.Id);
        Assert.Equal("Test Phone", dto.Name);
        Assert.Equal(999.99m, dto.Price);
        Assert.Equal(4, dto.StockQuantity);
    }

    [Fact]
    public void Map_UsesCategoryIdAndName()
    {
        var dto = Map(CreateProduct(stock: 4));

        Assert.Equal(2, dto.CategoryId);
        Assert.Equal("Phones", dto.CategoryName);
    }

    [Fact]
    public void Map_WhenImageUrlIsMissing_KeepsItNull()
    {
        var dto = Map(CreateProduct(stock: 4, imageUrl: null));

        Assert.Null(dto.ImageUrl);
    }

    [Fact]
    public void Map_WhenImageUrlIsSet_CopiesIt()
    {
        var dto = Map(CreateProduct(stock: 4, imageUrl: "/images/phone.png"));

        Assert.Equal("/images/phone.png", dto.ImageUrl);
    }
}
