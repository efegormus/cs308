using OnlineStore.Models;

namespace OnlineStore.Tests;

public class ProductTests
{
    [Fact]
    public void EffectiveCost_DefaultsToHalfOfPrice()
    {
        var product = new Product { Sku = "IPHONE-15", Name = "iPhone 15", Price = 40000m };

        Assert.Null(product.Cost);
        Assert.Equal(20000m, product.EffectiveCost);
    }

    [Fact]
    public void EffectiveCost_UsesCostWhenProductManagerSetsIt()
    {
        var product = new Product { Sku = "IPAD-AIR", Name = "iPad Air", Price = 30000m, Cost = 21000m };

        Assert.Equal(21000m, product.EffectiveCost);
    }

    [Fact]
    public void EffectiveCost_RoundsToTwoDecimals()
    {
        var product = new Product { Sku = "CASE", Name = "Phone case", Price = 10.50m };

        Assert.Equal(5.25m, product.EffectiveCost);
    }

    [Fact]
    public void NewProduct_HasEmptyRequiredTextFields()
    {
        var product = new Product { Sku = "TAB-1", Name = "Tablet" };

        Assert.Equal("", product.Model);
        Assert.Equal("", product.SerialNumber);
        Assert.Equal("", product.WarrantyStatus);
        Assert.Equal("", product.DistributorInformation);
    }
}
