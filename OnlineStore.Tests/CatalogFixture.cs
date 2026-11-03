using OnlineStore.Models;

namespace OnlineStore.Tests;

internal static class CatalogFixture
{
    public static void CompleteDetails(IEnumerable<Category> categories)
    {
        foreach (var product in categories.SelectMany(category => category.Products)) CompleteDetails(product);
    }

    public static void CompleteDetails(Product product)
    {
        product.Model = "Fixture model";
        product.SerialNumber = "FIXTURE-" + product.Sku;
        product.Description ??= "Fixture product description";
        product.WarrantyStatus = "Test warranty";
        product.DistributorInformation = "Test distributor";
    }
}
