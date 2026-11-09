using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using OnlineStore.Data;
using OnlineStore.Models;

namespace OnlineStore.Invoicing;

public sealed class InvoiceEmailOutbox(StoreDbContext db, TimeProvider clock, ILogger<InvoiceEmailOutbox>? log = null, InvoiceService? invoices = null)
{
    public async Task EnqueueAsync(long orderId, CancellationToken ct)
    {
        if (invoices is not null) await invoices.EnsureAsync(orderId, ct);
        var invoice = await db.Invoices.Include(i => i.Order).SingleOrDefaultAsync(i => i.OrderId == orderId, ct);
        // The redesigned outbox requires an issued invoice. Reconciliation repairs post-commit gaps.
        if (invoice is null || invoice.Order.PaymentStatus != PaymentStatus.Confirmed) return;
        if (string.IsNullOrWhiteSpace(invoice.Order.EmailSnapshot)) throw new InvalidOperationException("Invoice recipient is missing.");
        var key = $"invoice:{invoice.Id}";
        if (await db.InvoiceEmailDeliveries.AnyAsync(x => x.DedupKey == key, ct)) return;
        var now = clock.GetUtcNow().UtcDateTime;
        var delivery = new InvoiceEmailDelivery
        {
            Invoice = invoice, InvoiceId = invoice.Id, DedupKey = key, Kind = "InvoiceEmail",
            RecipientUserId = invoice.Order.UserId, RecipientSnapshot = invoice.Order.EmailSnapshot!,
            CreatedAt = now, NextAttemptAt = now
        };
        db.InvoiceEmailDeliveries.Add(delivery);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException e) when (e.InnerException is MySqlException { Number: 1062 })
        { db.Entry(delivery).State = EntityState.Detached; }
        catch { db.Entry(delivery).State = EntityState.Detached; throw; }
    }

    public async Task ReconcileAsync(CancellationToken ct)
    {
        var ids = await db.Orders.AsNoTracking().Where(o => o.PaymentStatus == PaymentStatus.Confirmed
            && o.PurchasedAt != null && !db.InvoiceEmailDeliveries.Any(d => d.Invoice != null && d.Invoice.OrderId == o.Id))
            .OrderBy(o => o.Id).Select(o => o.Id).Take(50).ToListAsync(ct);
        foreach (var id in ids)
        {
            ct.ThrowIfCancellationRequested();
            try { await EnqueueAsync(id, ct); }
            catch (Exception) when (!ct.IsCancellationRequested)
            { log?.LogWarning("Invoice email reconciliation skipped order {OrderId}: reconciliation_failure", id); }
        }
    }
}
