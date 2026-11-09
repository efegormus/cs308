namespace OnlineStore.Seeding;

public static class SeedCatalogValidator
{
    public static IReadOnlyList<string> Validate(SeedCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        var errors = new List<string>();
        var categoryCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var serialNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (catalog.Categories.Count == 0)
            errors.Add("No categories were provided.");
        if (catalog.Products.Count == 0)
            errors.Add("No products were provided.");

        foreach (var category in catalog.Categories)
        {
            if (string.IsNullOrWhiteSpace(category.Code))
                errors.Add("A category has an empty code.");
            else if (!categoryCodes.Add(category.Code))
                errors.Add($"Duplicate category code: {category.Code}.");

            if (string.IsNullOrWhiteSpace(category.Name))
                errors.Add($"Category {category.Code} has an empty name.");
        }

        foreach (var category in catalog.Categories)
        {
            if (category.ParentCode is not null && !categoryCodes.Contains(category.ParentCode))
                errors.Add($"Category {category.Code} references an unknown parent.");
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { category.Code };
            var parent = category.ParentCode;
            while (parent is not null)
            {
                if (!visited.Add(parent)) { errors.Add($"Category cycle at {category.Code}."); break; }
                parent = catalog.Categories.FirstOrDefault(c => c.Code.Equals(parent, StringComparison.OrdinalIgnoreCase))?.ParentCode;
            }
        }
        var types = new Dictionary<string, string>();

        foreach (var product in catalog.Products)
        {
            foreach (var spec in product.Specifications)
            {
                var valid = spec.DataType switch
                {
                    "Text" => spec.Text is not null && spec.Number is null && spec.Bool is null,
                    "Number" => spec.Number is not null && spec.Text is null && spec.Bool is null,
                    "Bool" => spec.Bool is not null && spec.Text is null && spec.Number is null,
                    _ => false
                };
                if (!valid || string.IsNullOrWhiteSpace(spec.Code) || string.IsNullOrWhiteSpace(spec.Label))
                    errors.Add($"Invalid specification on {product.SeedKey}.");
                var definitionKey = product.CategoryCode + ":" + spec.Code;
                if (types.TryGetValue(definitionKey, out var type) && type != spec.DataType)
                    errors.Add($"Conflicting specification type: {definitionKey}.");
                types[definitionKey] = spec.DataType;
            }
            if (product.Specifications.Select(s => s.Code).Distinct().Count() != product.Specifications.Count)
                errors.Add($"Duplicate specification on {product.SeedKey}.");
            var key = string.IsNullOrWhiteSpace(product.SeedKey) ? "<missing-key>" : product.SeedKey;

            if (string.IsNullOrWhiteSpace(product.SeedKey))
                errors.Add("A product has an empty seed key.");
            else if (!seedKeys.Add(product.SeedKey))
                errors.Add($"Duplicate seed key: {product.SeedKey}.");

            if (string.IsNullOrWhiteSpace(product.Name))
                errors.Add($"Product {key} has an empty name.");
            if (string.IsNullOrWhiteSpace(product.Model))
                errors.Add($"Product {key} has an empty model.");
            if (string.IsNullOrWhiteSpace(product.Description))
                errors.Add($"Product {key} has an empty description.");
            if (string.IsNullOrWhiteSpace(product.SerialNumber))
                errors.Add($"Product {key} has an empty serial number.");
            else if (!serialNumbers.Add(product.SerialNumber))
                errors.Add($"Duplicate serial number: {product.SerialNumber}.");
            if (string.IsNullOrWhiteSpace(product.WarrantyStatus))
                errors.Add($"Product {key} has no warranty status.");
            if (string.IsNullOrWhiteSpace(product.DistributorInformation))
                errors.Add($"Product {key} has no distributor information.");

            if (product.Price <= 0)
                errors.Add($"Product {key} has an invalid price.");
            if (product.QuantityInStock < 0)
                errors.Add($"Product {key} has negative stock.");
            if (product.Cost < 0 || product.Cost > product.Price)
                errors.Add($"Product {key} has an invalid sample cost.");
            if (string.IsNullOrWhiteSpace(product.CategoryCode) ||
                !categoryCodes.Contains(product.CategoryCode))
                errors.Add($"Product {key} references an unknown category: {product.CategoryCode}.");
        }

        return errors;
    }
}
