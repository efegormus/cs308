namespace OnlineStore.Invoicing;

public sealed record InvoiceEmailMessage(long OrderId, string Recipient, InvoiceFile Attachment)
{
    public string MessageId => $"<invoice-order-{OrderId}@onlinestore.invalid>";
}

public interface IInvoiceEmailSender
{
    Task SendAsync(InvoiceEmailMessage message, CancellationToken ct);
}
