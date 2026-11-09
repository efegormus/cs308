namespace OnlineStore.Models;

// One invoice per paid order.
public partial class Invoice
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public required string InvoiceNumber { get; set; }

    // Customer data as it was when the invoice was issued. A later profile edit never changes an issued invoice.
    public required string CustomerNameSnapshot { get; set; }
    public required string TaxIdSnapshot { get; set; }
    public required string AddressSnapshot { get; set; }

    // Where the PDF is kept (outside wwwroot). The storage choice belongs to SCRUM-74.
    public string? ProtectedDocumentKey { get; set; }
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public DateTime? EmailedAt => EmailDeliveries.Where(e => e.Status == "Sent").Select(e => e.SentAt).FirstOrDefault();
    public ICollection<InvoiceEmailDelivery> EmailDeliveries { get; set; } = new List<InvoiceEmailDelivery>();

    public Order Order { get; set; } = null!;
}
