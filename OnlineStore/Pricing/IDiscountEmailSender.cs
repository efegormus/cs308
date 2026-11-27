namespace OnlineStore.Pricing;

// SCRUM-85: one plain-text email about a discount. The transport (local sink or SMTP) is chosen with the
// same Email:Transport setting as the invoice emails (SCRUM-75), so a deployment configures mail once.
public sealed record DiscountEmailMessage(string Recipient, string Subject, string Body, string MessageId);

public interface IDiscountEmailSender
{
    Task SendAsync(DiscountEmailMessage message, CancellationToken ct);
}

// Development-only sink. Keeps the latest messages in memory; nothing is logged or exposed by an API.
public sealed class LocalDiscountEmailSender : IDiscountEmailSender
{
    public const int RetentionLimit = 20;
    private readonly Queue<DiscountEmailMessage> messages = new();
    private readonly object gate = new();
    public IReadOnlyList<DiscountEmailMessage> Messages { get { lock (gate) return messages.ToArray(); } }

    public Task SendAsync(DiscountEmailMessage message, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (gate)
        {
            if (messages.Count == RetentionLimit) messages.Dequeue();
            messages.Enqueue(message);
        }
        return Task.CompletedTask;
    }
}
