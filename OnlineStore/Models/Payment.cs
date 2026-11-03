namespace OnlineStore.Models;

// One payment attempt for an order. No card data is stored here:
// the card is only checked by the mock bank and then discarded.
public class Payment
{
    public int Id { get; set; }
    public int OrderId { get; set; }

    // Sent with the request so retrying the same payment never charges twice.
    public required string IdempotencyKey { get; set; }
    public string? ProviderReference { get; set; }
    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ConfirmedAt { get; set; }

    public Order Order { get; set; } = null!;
}
