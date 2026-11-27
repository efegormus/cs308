using System.Text.Json.Serialization;

namespace OnlineStore.Pricing;

// SCRUM-155: request and response shapes of the sales manager's pricing and discount API.

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PriceChange(decimal? Price)
{
    // Product.Price is decimal(18,2); this keeps the list price a sensible shop price.
    public const decimal MaxPrice = 9_999_999.99m;

    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        if (Price is null || Price <= 0 || Price > MaxPrice || decimal.Round(Price.Value, 2) != Price.Value)
            errors["price"] = [$"Use a price above 0 and up to {MaxPrice} with at most 2 decimals."];
        return errors;
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DiscountRequest(string? Name, decimal? Rate, DateTimeOffset? StartsAt, DateTimeOffset? EndsAt,
    long[]? ProductIds)
{
    public const int MaxNameLength = 100;
    public const int MaxProducts = 200;

    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(Name) || Name.Trim().Length > MaxNameLength)
            errors["name"] = [$"Use a non-blank name of at most {MaxNameLength} characters."];
        // 0.15 means 15% off. The column is decimal(5,4), so more than 4 decimals would be silently rounded.
        if (Rate is null || Rate <= 0 || Rate >= 1 || decimal.Round(Rate.Value, 4) != Rate.Value)
            errors["rate"] = ["Use a rate above 0 and below 1 with at most 4 decimals (0.15 means 15% off)."];
        if (EndsAt is null) errors["endsAt"] = ["Give the date and time the discount ends."];
        else if (StartsAt is not null && EndsAt <= StartsAt) errors["endsAt"] = ["The discount must end after it starts."];
        if (ProductIds is null || ProductIds.Length == 0 || ProductIds.Length > MaxProducts
            || ProductIds.Any(id => id <= 0) || ProductIds.Distinct().Count() != ProductIds.Length)
            errors["productIds"] = [$"Give 1 to {MaxProducts} distinct, positive product ids."];
        return errors;
    }
}

// The sales screen lists every product with its list price, the price payable now and the active rate.
public sealed record SalesProduct(long Id, string Name, string Model, int StockQuantity, bool IsActive,
    decimal ListPrice, decimal Price, decimal DiscountRate);
public sealed record SalesProductPage(IReadOnlyList<SalesProduct> Products, long? NextAfterId);

// Status is Scheduled, Active, Expired or Cancelled (computed from the clock and CancelledAt).
public sealed record DiscountView(long Id, string Name, decimal Rate, DateTime StartsAt, DateTime EndsAt, string Status,
    DateTime? CancelledAt, IReadOnlyList<long> ProductIds);
public sealed record DiscountPage(IReadOnlyList<DiscountView> Discounts, long? NextBeforeId);
