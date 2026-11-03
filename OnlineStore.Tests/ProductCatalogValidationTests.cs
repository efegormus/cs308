using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using OnlineStore.Data;
using OnlineStore.Models;

namespace OnlineStore.Tests;

public class ProductCatalogValidationTests
{
    private static Product Complete()
    {
        var product = new Product { Sku = "PHONE", Name = "Phone", Category = new Category { Name = "Phones" } };
        CatalogFixture.CompleteDetails(product);
        return product;
    }

    [Theory]
    [InlineData("sku")]
    [InlineData("name")]
    [InlineData("model")]
    [InlineData("serialNumber")]
    [InlineData("description")]
    [InlineData("warrantyStatus")]
    [InlineData("distributorInformation")]
    public void ActiveProducts_RequireEveryCatalogField(string field)
    {
        var product = Complete();
        switch (field)
        {
            case "sku": product.Sku = " "; break;
            case "name": product.Name = " "; break;
            case "model": product.Model = " "; break;
            case "serialNumber": product.SerialNumber = " "; break;
            case "description": product.Description = null; break;
            case "warrantyStatus": product.WarrantyStatus = "\t\r\n"; break;
            default: product.DistributorInformation = " "; break;
        }
        Assert.Contains(field, ProductCatalogValidation.Validate(product).Keys);
    }

    [Fact]
    public void ActiveProducts_RejectOverlongBoundedFields()
    {
        var product = Complete();
        product.Model = new string('x', 101);
        Assert.Contains("model", ProductCatalogValidation.Validate(product).Keys);
    }

    [Fact]
    public async Task Context_RejectsIncompleteActiveProductsBeforeSaving()
    {
        using var db = new StoreDbContext(new DbContextOptionsBuilder<StoreDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Products.Add(new Product { Sku = "PHONE", Name = "Incomplete" });
        await Assert.ThrowsAsync<ValidationException>(() => db.SaveChangesAsync());
        Assert.Empty(await db.Products.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task InactiveLegacyRecords_AreRetainedButCannotBeReactivatedIncomplete()
    {
        using var db = new StoreDbContext(new DbContextOptionsBuilder<StoreDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var product = new Product { Sku = "LEGACY", Name = "Legacy", IsActive = false };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        product.IsActive = true;
        await Assert.ThrowsAsync<ValidationException>(() => db.SaveChangesAsync());
        CatalogFixture.CompleteDetails(product);
        await db.SaveChangesAsync();
        Assert.True((await db.Products.SingleAsync()).IsActive);
    }
}
