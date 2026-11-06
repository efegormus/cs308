namespace OnlineStore.Models;

public sealed class InvoiceEmailDelivery
{
    public int OrderId { get; init; }
    public string RecipientSnapshot { get; init; } = "";
    public int AttemptCount { get; set; }
    public DateTime CreatedAt { get; init; }
    public DateTime NextAttemptAt { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? LeaseUntil { get; set; }
    public string? LeaseToken { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? StoppedAt { get; set; }
    public string? LastErrorCode { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string Status => SentAt != null ? "Sent" : StoppedAt != null ? "Stopped"
        : LastErrorCode != null ? "Retrying" : "Pending";
}
