namespace OnlineStore.Dtos;

// One line of the cart as the API returns it. `Available` is false when the product was
// deactivated or its stock dropped below the quantity in the cart.
public record CartLineDto(
    long ProductId,
    string Name,
    decimal UnitPrice,
    int Quantity,
    int StockQuantity,
    string? ImageUrl,
    decimal LineTotal,
    bool Available);

public record CartDto(IReadOnlyList<CartLineDto> Items, int ItemCount, decimal Total)
{
    public static CartDto Empty => new(Array.Empty<CartLineDto>(), 0, 0m);

    public static CartDto FromLines(IReadOnlyList<CartLineDto> lines) =>
        new(lines, lines.Sum(line => line.Quantity), lines.Sum(line => line.LineTotal));
}
