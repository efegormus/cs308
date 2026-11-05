namespace OnlineStore.Dtos;

// A line that could not be merged cleanly (SCRUM-100). The selection is kept; this only tells the customer.
// Reason is "Unavailable" (the product is inactive), "NotEnoughStock" (the cart now holds more than the stock)
// or "CartFull" (the account cart already has the maximum number of lines, so the line stayed in the guest cart).
public record CartMergeConflictDto(int ProductId, string Name, string Reason, int Quantity, int StockQuantity);

// The answer of POST /api/cart/merge. Merged is false when there was no guest cart to move.
public record CartMergeDto(CartDto Cart, bool Merged, IReadOnlyList<CartMergeConflictDto> Conflicts);
