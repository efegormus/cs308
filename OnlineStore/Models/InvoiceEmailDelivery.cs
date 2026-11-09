namespace OnlineStore.Models;

public sealed partial class InvoiceEmailDelivery
{
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public long OrderId { get => Invoice?.OrderId ?? orderId; init => orderId = value; }
    private long orderId;
    public Invoice? Invoice { get; set; }
    public string RecipientSnapshot { get; init; } = "";
    public int AttemptCount { get; set; }
    public DateTime CreatedAt { get; init; }
    public DateTime NextAttemptAt { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? LeaseUntil { get; set; }
    public string? LeaseToken { get; set; }
    public DateTime? SentAt { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public DateTime? StoppedAt { get => Status == "Failed" ? LastAttemptAt : null; set { if (value is not null) { Status = "Failed"; LastAttemptAt = value; } } }
    public string? LastErrorCode { get; set; }

}
