using System.Text.Json.Serialization;

namespace OnlineStore.Catalog;

// SCRUM-77: request and response shapes of the product manager's product and category API.

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CategoryCreate(string? Name, string? Description, long? ParentId)
{
    public Dictionary<string, string[]> Validate()
    {
        var errors = CategoryRules.Validate(Name, Description);
        if (ParentId is <= 0) errors["parentId"] = ["Use a positive parent category id, or leave it out."];
        return errors;
    }
}

// A category keeps its place in the tree: only the name and the description change.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CategoryEdit(string? Name, string? Description)
{
    public Dictionary<string, string[]> Validate() => CategoryRules.Validate(Name, Description);
}

public static class CategoryRules
{
    public const int MaxNameLength = 100;
    public const int MaxDescriptionLength = 1000;

    public static Dictionary<string, string[]> Validate(string? name, string? description)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > MaxNameLength)
            errors["name"] = [$"Use a non-blank name of at most {MaxNameLength} characters."];
        if (description is not null && description.Trim().Length > MaxDescriptionLength)
            errors["description"] = [$"Use a description of at most {MaxDescriptionLength} characters."];
        return errors;
    }
}

// The fields every product has (Course Project requirement 3). The stock count is set when a product is created and
// is managed on its own afterwards (SCRUM-78), so the edit shape below does not carry it.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ProductCreate(long? CategoryId, long? BrandId, string? Sku, string? Name, string? Model,
    string? SerialNumber, string? Description, decimal? Price, int? StockQuantity, string? WarrantyStatus,
    string? DistributorName, string? DistributorContact)
{
    public Dictionary<string, string[]> Validate()
    {
        var errors = ProductRules.Validate(CategoryId, BrandId, Sku, Name, Model, SerialNumber, Description, Price,
            WarrantyStatus, DistributorName, DistributorContact);
        if (StockQuantity is null || StockQuantity < 0 || StockQuantity > ProductRules.MaxStock)
            errors["stockQuantity"] = [$"Use a whole stock count from 0 to {ProductRules.MaxStock}."];
        return errors;
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ProductEdit(long? CategoryId, long? BrandId, string? Sku, string? Name, string? Model,
    string? SerialNumber, string? Description, decimal? Price, string? WarrantyStatus, string? DistributorName,
    string? DistributorContact, bool? IsActive)
{
    public Dictionary<string, string[]> Validate()
    {
        var errors = ProductRules.Validate(CategoryId, BrandId, Sku, Name, Model, SerialNumber, Description, Price,
            WarrantyStatus, DistributorName, DistributorContact);
        if (IsActive is null) errors["isActive"] = ["Say whether the product is on sale (true or false)."];
        return errors;
    }
}

public static class ProductRules
{
    // The limits are the column sizes in the database (docs/db-redesign). Product.Price is decimal(18,2).
    public const int MaxSku = 64, MaxName = 200, MaxModel = 100, MaxSerial = 100, MaxWarranty = 100,
        MaxDistributorName = 150, MaxDistributorContact = 500, MaxDescription = 5000, MaxStock = 1_000_000;
    public const decimal MaxPrice = 9_999_999.99m;

    public static Dictionary<string, string[]> Validate(long? categoryId, long? brandId, string? sku, string? name,
        string? model, string? serialNumber, string? description, decimal? price, string? warrantyStatus,
        string? distributorName, string? distributorContact)
    {
        var errors = new Dictionary<string, string[]>();
        if (categoryId is null or <= 0) errors["categoryId"] = ["Choose a category."];
        if (brandId is <= 0) errors["brandId"] = ["Use a positive brand id, or leave it out."];
        Text(errors, "sku", sku, MaxSku);
        Text(errors, "name", name, MaxName);
        Text(errors, "model", model, MaxModel);
        Text(errors, "serialNumber", serialNumber, MaxSerial);
        Text(errors, "description", description, MaxDescription);
        Text(errors, "warrantyStatus", warrantyStatus, MaxWarranty);
        Text(errors, "distributorName", distributorName, MaxDistributorName);
        Text(errors, "distributorContact", distributorContact, MaxDistributorContact);
        if (sku is not null && sku.Trim().Any(char.IsWhiteSpace))
            errors["sku"] = ["The SKU cannot contain spaces."];
        if (price is null || price < 0 || price > MaxPrice || decimal.Round(price.Value, 2) != price.Value)
            errors["price"] = [$"Use a price from 0 to {MaxPrice} with at most 2 decimals."];
        return errors;
    }

    private static void Text(Dictionary<string, string[]> errors, string field, string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > max)
            errors[field] = [$"Enter a value of 1 to {max} characters."];
    }
}

public sealed record ManagedCategory(long Id, long? ParentId, string Name, string Slug, string? Description,
    bool IsActive, int ProductCount, int ChildCount);

public sealed record ManagedProduct(long Id, long CategoryId, string CategoryName, long? BrandId, string Sku,
    string Name, string Model, string SerialNumber, string Description, decimal Price, int StockQuantity,
    string WarrantyStatus, string DistributorName, string DistributorContact, bool IsActive);
public sealed record ManagedProductPage(IReadOnlyList<ManagedProduct> Products, long? NextAfterId);
