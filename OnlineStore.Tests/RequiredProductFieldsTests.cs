using System.Text.Json;
using OnlineStore.Catalog;
using OnlineStore.Dtos;
using OnlineStore.Models;

namespace OnlineStore.Tests;

public class RequiredProductFieldsTests
{
    private static Product Product()
    {
        var p = new Product { Id = 2147483648, Sku = "INTERNAL-SKU", Name = "Fixture phone", Price = 100, StockQuantity = 3,
            Category = new Category { Id = 1, Name = "Phones" } };
        CatalogFixture.CompleteDetails(p);p.SerialNumber="VISIBLE-SERIAL-001";return p;
    }
    [Fact] public void DetailPreservesIdentityAndSerialExactly()
    {
        var p=Product();var dto=ProductDetailDto.FromProduct.Compile()(p);
        Assert.Equal(p.Id,dto.Id);Assert.Equal(p.SerialNumber,dto.SerialNumber);Assert.Equal(p.Model,dto.Model);
    }
    [Fact] public void AllNineRequiredFieldsReachThePublicContract()
    {
        var p=Product();var dto=ProductDetailDto.FromProduct.Compile()(p);
        using var json=JsonDocument.Parse(JsonSerializer.Serialize(dto,new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        foreach(var field in new[]{"id","name","model","serialNumber","description","stockQuantity","price","warrantyStatus","distributorInformation"})
            Assert.NotEqual(JsonValueKind.Null,json.RootElement.GetProperty(field).ValueKind);
    }
    [Fact] public void PrivateInventoryPropertiesAreNotExposed()
    {
        var dto=ProductDetailDto.FromProduct.Compile()(Product());
        var names=typeof(ProductDetailDto).GetProperties().Select(p=>p.Name).ToArray();
        Assert.DoesNotContain("Sku",names);Assert.DoesNotContain("UnitCost",names);Assert.DoesNotContain("RowVersion",names);
        Assert.DoesNotContain("INTERNAL-SKU",JsonSerializer.Serialize(dto));
    }
    [Fact] public void DiscountMappingPreservesRequiredProductDetails()
    {
        var p=Product();var dto=ProductDetailDto.FromPricedProduct.Compile()(new PricedProduct{Product=p,Price=90,ListPrice=100,DiscountRate=.1m});
        Assert.Equal(90,dto.Price);Assert.Equal(p.SerialNumber,dto.SerialNumber);Assert.Equal(p.WarrantyStatus,dto.WarrantyStatus);Assert.Equal(p.DistributorInformation,dto.DistributorInformation);
    }
    [Theory][InlineData(0,false)][InlineData(12,true)]
    public void StockQuantityRemainsVisibleEvenWhenSoldOut(int stock,bool available)
    {
        var p=Product();p.StockQuantity=stock;var dto=ProductDetailDto.FromProduct.Compile()(p);
        Assert.Equal(stock,dto.StockQuantity);Assert.Equal(available,dto.InStock);
    }
}
