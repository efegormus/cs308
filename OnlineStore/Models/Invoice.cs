namespace OnlineStore.Models;

// One invoice per paid order.
public class Invoice
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public required string InvoiceNumber { get; set; }

    // Customer data as it was when the invoice was issued. A later profile edit never changes an issued invoice.
    public required string CustomerNameSnapshot { get; set; }
    public required string TaxIdSnapshot { get; set; }
    public required string AddressSnapshot { get; set; }

    // Where the PDF is kept (outside wwwroot). The storage choice belongs to SCRUM-74.
    public string? ProtectedDocumentKey { get; set; }
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EmailedAt { get; set; }

    public Order Order { get; set; } = null!;
}
