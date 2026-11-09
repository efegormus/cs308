namespace OnlineStore.Models;

public static class ProductCatalogValidation
{
    public static Dictionary<string, string[]> Validate(Product product)
    {
        var errors = new Dictionary<string, string[]>();
        if (product.Price < 0) errors["price"] = ["Price cannot be negative."];
        if (product.Cost < 0) errors["cost"] = ["Cost cannot be negative."];
        if (product.StockQuantity < 0) errors["stockQuantity"] = ["Stock cannot be negative."];
        if (!product.IsActive) return errors;
        Required(product.Sku, "sku", 64, errors);
        Required(product.Name, "name", 200, errors);
        Required(product.Model, "model", 100, errors);
        Required(product.SerialNumber, "serialNumber", 100, errors);
        Required(product.Description, "description", null, errors);
        Required(product.WarrantyStatus, "warrantyStatus", 100, errors);
        Required(product.DistributorInformation, "distributorInformation", 500, errors);
        return errors;
    }

    private static void Required(string? value, string field, int? maxLength, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(value)) errors[field] = ["Active products require this field."];
        else if (maxLength is { } limit && value.Length > limit) errors[field] = [$"Use at most {limit} characters."];
    }
}
