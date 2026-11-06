namespace OnlineStore.Invoicing;

// Development-only sink. Contents are intentionally never written to logs or exposed by an API.
public sealed class LocalInvoiceEmailSender : IInvoiceEmailSender
{
    public const int RetentionLimit = 20;
    private readonly Queue<InvoiceEmailMessage> messages = new();
    private readonly object gate = new();
    public IReadOnlyList<InvoiceEmailMessage> Messages { get { lock (gate) return messages.ToArray(); } }
    public Task SendAsync(InvoiceEmailMessage message, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (gate)
        {
            if (messages.Count == RetentionLimit) messages.Dequeue();
            // One defensive copy at ingestion protects the retained PDF from caller mutation.
            // Inspection copies only the queue's references, never the PDF byte arrays.
            messages.Enqueue(message with { Attachment = message.Attachment with { Content = message.Attachment.Content.ToArray() } });
        }
        return Task.CompletedTask;
    }
}
