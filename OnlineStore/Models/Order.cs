namespace OnlineStore.Models;

public class Order
{
    public int Id { get; set; }
    public int UserId { get; set; }
    // A cart can produce at most one purchase, even with different retry keys.
    public int? SourceCartId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public OrderStatus Status { get; private set; } = OrderStatus.Processing;
    public PaymentStatus PaymentStatus { get; private set; } = PaymentStatus.Pending;
    public DateTime? PurchasedAt { get; private set; }
    public DateTime? DeliveredAt { get; private set; }

    // Customer data as it was at purchase time. Later profile edits do not change old orders.
    public string? CustomerNameSnapshot { get; set; }
    public string? TaxIdSnapshot { get; set; }
    public string? EmailSnapshot { get; set; }
    public string? AddressSnapshot { get; set; }

    // Updated by MySQL on every change, so two users cannot silently overwrite each other's update.
    public DateTime RowVersion { get; set; }

    public User User { get; set; } = null!;
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public Invoice? Invoice { get; set; }
    public ICollection<Delivery> Deliveries { get; set; } = new List<Delivery>();

    // The checkout service must persist this with guarded stock updates and snapshots in one transaction.
    public bool ConfirmPayment(DateTime purchasedAtUtc)
    {
        RequireUtc(purchasedAtUtc);
        if (Status != OrderStatus.Processing || PaymentStatus is not (PaymentStatus.Pending or PaymentStatus.Failed))
            return false;
        PaymentStatus = PaymentStatus.Confirmed;
        PurchasedAt = purchasedAtUtc;
        return true;
    }

    public bool RecordPaymentFailure()
    {
        if (Status != OrderStatus.Processing || PaymentStatus != PaymentStatus.Pending) return false;
        PaymentStatus = PaymentStatus.Failed;
        return true;
    }

    public bool TransitionTo(OrderStatus target, DateTime occurredAtUtc)
    {
        RequireUtc(occurredAtUtc);
        if (!Enum.IsDefined(target)) throw new ArgumentOutOfRangeException(nameof(target));
        var allowed = (Status, target) switch
        {
            (OrderStatus.Processing, OrderStatus.Cancelled) => true,
            (OrderStatus.Processing, OrderStatus.InTransit) => PaymentStatus == PaymentStatus.Confirmed,
            (OrderStatus.InTransit, OrderStatus.Delivered) => PaymentStatus == PaymentStatus.Confirmed,
            _ => false
        };
        if (!allowed || (PurchasedAt is { } purchased && occurredAtUtc < purchased)) return false;
        Status = target;
        if (target == OrderStatus.Delivered) DeliveredAt = occurredAtUtc;
        return true;
    }

    public bool CanRequestReturn(DateTime requestedAtUtc)
    {
        RequireUtc(requestedAtUtc);
        return Status == OrderStatus.Delivered && PaymentStatus == PaymentStatus.Confirmed
            && PurchasedAt is { } purchased && requestedAtUtc >= purchased
            && requestedAtUtc - purchased <= TimeSpan.FromDays(30)
            && (DeliveredAt is null || requestedAtUtc >= DeliveredAt);
    }

    private static void RequireUtc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc) throw new ArgumentException("Order timestamps must be UTC.");
    }
}
