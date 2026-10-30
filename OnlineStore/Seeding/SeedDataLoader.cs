using Microsoft.EntityFrameworkCore;
using OnlineStore.Data;
using OnlineStore.Models;

namespace OnlineStore.Seeding;

// How many new rows a seed run added (0 and 0 when everything already existed).
public sealed record SeedResult(int CategoriesAdded, int ProductsAdded);

public static class SeedDataLoader
{
    // Writes the sample catalog into the database. Safe to run again:
    // categories are matched by name and products by SKU; existing rows are left untouched.
    public static async Task<SeedResult> LoadAsync(
        StoreDbContext db, SeedCatalog catalog, CancellationToken cancellationToken = default)
    {
        // Refuse to write anything if the sample data itself is broken.
        var errors = SeedCatalogValidator.Validate(catalog);
        if (errors.Count > 0)
        {
            throw new InvalidDataException("Seed data is invalid: " + string.Join(" ", errors));
        }

        var categoriesByName = await db.Categories
            .ToDictionaryAsync(category => category.Name, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var categoriesByCode = new Dictionary<string, Category>(StringComparer.OrdinalIgnoreCase);
        var categoriesAdded = 0;

        foreach (var seedCategory in catalog.Categories)
        {
            if (!categoriesByName.TryGetValue(seedCategory.Name, out var category))
            {
                category = new Category { Name = seedCategory.Name };
                db.Categories.Add(category);
                categoriesByName[seedCategory.Name] = category;
                categoriesAdded++;
            }

            categoriesByCode[seedCategory.Code] = category;
        }

        // The seed key becomes the product SKU, which is unique in the database.
        var existingSkus = new HashSet<string>(
            await db.Products.Select(product => product.Sku).ToListAsync(cancellationToken),
            StringComparer.OrdinalIgnoreCase);
        var productsAdded = 0;

        foreach (var seedProduct in catalog.Products)
        {
            if (existingSkus.Contains(seedProduct.SeedKey))
            {
                continue;
            }

            db.Products.Add(new Product
            {
                Sku = seedProduct.SeedKey,
                Name = seedProduct.Name,
                Model = seedProduct.Model,
                SerialNumber = seedProduct.SerialNumber,
                Description = seedProduct.Description,
                Price = seedProduct.Price,
                StockQuantity = seedProduct.QuantityInStock,
                WarrantyStatus = seedProduct.WarrantyStatus,
                DistributorInformation = seedProduct.DistributorInformation,
                Cost = seedProduct.Cost,
                Category = categoriesByCode[seedProduct.CategoryCode]
            });
            productsAdded++;
        }

        // One SaveChanges is one transaction: either everything is written or nothing.
        await db.SaveChangesAsync(cancellationToken);
        return new SeedResult(categoriesAdded, productsAdded);
    }
}
