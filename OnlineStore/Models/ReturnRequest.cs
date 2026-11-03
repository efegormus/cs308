namespace OnlineStore.Models;

// A customer's request to return (part of) a delivered order line.
public class ReturnRequest
{
    public int Id { get; set; }
    public int OrderItemId { get; set; }
    public int UserId { get; set; }
    public int? ReviewedByUserId { get; set; }
    public int Quantity { get; set; }
    public ReturnStatus Status { get; set; } = ReturnStatus.Requested;
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReceivedAt { get; set; }

    // Updated by MySQL on every change, so two managers cannot both approve the same request.
    public DateTime RowVersion { get; set; }

    public OrderItem OrderItem { get; set; } = null!;
    public Refund? Refund { get; set; }
}

// Money given back for an approved return. Refund amount = the price paid at purchase time x quantity.
public class Refund
{
    public int Id { get; set; }
    public int ReturnRequestId { get; set; }
    public int PaymentId { get; set; }

    // Makes sure a retried refund is never paid twice.
    public required string IdempotencyKey { get; set; }
    public decimal Amount { get; set; }
    public DateTime RefundedAt { get; set; } = DateTime.UtcNow;

    public ReturnRequest ReturnRequest { get; set; } = null!;
    public Payment Payment { get; set; } = null!;
}
