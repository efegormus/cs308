namespace OnlineStore.Models;

// A shipment of an order. The product manager's delivery list is built from these rows.
public partial class Delivery
{
    public long Id { get; set; }
    public long OrderId { get; set; }

    // Address as it was at purchase time.
    public required string AddressSnapshot { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Processing;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeliveredAt { get; set; }

    public Order Order { get; set; } = null!;
    public ICollection<DeliveryItem> Items { get; set; } = new List<DeliveryItem>();
}

public partial class DeliveryItem
{
    public long DeliveryId { get; set; }
    public long OrderItemId { get; set; }
    public int Quantity { get; set; }
    public decimal TotalPriceSnapshot { get; set; }

    public Delivery Delivery { get; set; } = null!;
    public OrderItem OrderItem { get; set; } = null!;
}
