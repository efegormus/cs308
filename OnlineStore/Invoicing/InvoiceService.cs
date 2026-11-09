using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using MySqlConnector;
using OnlineStore.Data;
using OnlineStore.Models;

namespace OnlineStore.Invoicing;

public sealed record InvoiceFile(string FileName, byte[] Content);

// Issues exactly one invoice per paid order and serves its PDF. Safe to call again after any failure.
public sealed class InvoiceService(StoreDbContext db, IInvoiceStorage storage, TimeProvider clock)
{
    public const string Currency = "TRY";

    public static string NumberFor(Order order) =>
        $"INV-{order.PurchasedAt!.Value.Year}-{order.Id:D6}";

    // Returns false when the order does not exist or is not paid. Never touches stock, payment or order status.
    public async Task<bool> EnsureAsync(long orderId, CancellationToken ct)
    {
        var order = await LoadAsync(orderId, ct);
        if (order is null || order.PaymentStatus != PaymentStatus.Confirmed || order.PurchasedAt is null) return false;

        var invoice = order.Invoice;
        if (invoice is null)
        {
            var instant = clock.GetUtcNow().UtcDateTime;
            invoice = new Invoice
            {
                OrderId = order.Id, InvoiceNumber = NumberFor(order),
                CustomerNameSnapshot = order.CustomerNameSnapshot ?? "",
                TaxIdSnapshot = order.TaxIdSnapshot ?? "",
                AddressSnapshot = order.AddressSnapshot ?? "",
                IssuedAt = instant.AddTicks(-(instant.Ticks % 10)),
                Subtotal = order.Subtotal, DiscountTotal = order.DiscountTotal,
                ShippingTotal = order.ShippingTotal, GrandTotal = order.GrandTotal,
                ProtectedDocumentKey = $"{NumberFor(order)}.pdf"
            };
            var document = InvoiceDocument.Render(DataFor(order, invoice));
            invoice.PdfSha256 = SHA256.HashData(document);
            await storage.SaveAsync(invoice.ProtectedDocumentKey!, document, ct);
            db.Invoices.Add(invoice);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException error) when (error.InnerException is MySqlException { Number: 1062 })
            {
                // A concurrent request issued the invoice first (unique OrderId/InvoiceNumber). Use that one.
                db.ChangeTracker.Clear();
                order = await LoadAsync(orderId, ct);
                invoice = order?.Invoice ?? throw new InvalidOperationException("The invoice could not be issued.");
            }
        }

        // The PDF is rebuilt from the stored snapshots, so a retry always produces the same document.
        var key = $"{invoice.InvoiceNumber}.pdf";
        if (invoice.ProtectedDocumentKey == key && await storage.ReadAsync(key, ct) is not null) return true;
        var repaired = InvoiceDocument.Render(DataFor(order!, invoice));
        await storage.SaveAsync(key, repaired, ct);
        invoice.PdfSha256 = SHA256.HashData(repaired);
        {
            invoice.ProtectedDocumentKey = key;
            await db.SaveChangesAsync(ct);
        }
        return true;
    }

    // Callers check who may read the order before calling this.
    public async Task<InvoiceFile?> GetPdfAsync(long orderId, CancellationToken ct)
    {
        if (!await EnsureAsync(orderId, ct)) return null;
        var invoice = await db.Invoices.AsNoTracking().SingleAsync(invoice => invoice.OrderId == orderId, ct);
        var content = await storage.ReadAsync(invoice.ProtectedDocumentKey!, ct)
            ?? throw new InvalidOperationException("The invoice document is missing.");
        return new InvoiceFile($"{invoice.InvoiceNumber}.pdf", content);
    }

    public static InvoiceData DataFor(Order order, Invoice invoice) => new(
        invoice.InvoiceNumber, order.Id, order.PurchasedAt!.Value, invoice.IssuedAt,
        invoice.CustomerNameSnapshot, invoice.TaxIdSnapshot, invoice.AddressSnapshot, Currency,
        order.OrderItems.OrderBy(line => line.ProductId)
            .Select(line => new InvoiceLine(line.ProductName, line.Quantity, line.UnitPrice)).ToList());

    private Task<Order?> LoadAsync(long orderId, CancellationToken ct) =>
        db.Orders.Include(order => order.OrderItems).Include(order => order.Invoice)
            .SingleOrDefaultAsync(order => order.Id == orderId, ct);
}
