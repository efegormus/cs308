using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using OnlineStore.Data;
using OnlineStore.Models;

namespace OnlineStore.Invoicing;

public sealed class InvoiceEmailOutbox(StoreDbContext db, TimeProvider clock, ILogger<InvoiceEmailOutbox>? log = null)
{
    public async Task EnqueueAsync(int orderId, CancellationToken ct)
    {
        if (await db.InvoiceEmailDeliveries.AnyAsync(x => x.OrderId == orderId, ct)) return;
        if (await db.Invoices.AnyAsync(x => x.OrderId == orderId && x.EmailedAt != null, ct)) return;
        var order = await db.Orders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == orderId, ct);
        if (order?.PaymentStatus != PaymentStatus.Confirmed || order.PurchasedAt is null) return;
        var now = clock.GetUtcNow().UtcDateTime;
        var delivery = new InvoiceEmailDelivery { OrderId = orderId, RecipientSnapshot = order.EmailSnapshot!, CreatedAt = now, NextAttemptAt = now };
        db.InvoiceEmailDeliveries.Add(delivery);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException e) when (e.InnerException is MySqlException { Number: 1062 })
        {
            db.Entry(delivery).State = EntityState.Detached;
        }
        catch
        {
            db.Entry(delivery).State = EntityState.Detached;
            throw;
        }
    }

    // Repairs the small post-commit enqueue gap after a process crash or database outage.
    public async Task ReconcileAsync(CancellationToken ct)
    {
        var ids = await db.Orders.AsNoTracking().Where(o => o.PaymentStatus == PaymentStatus.Confirmed
            && o.PurchasedAt != null && !db.InvoiceEmailDeliveries.Any(d => d.OrderId == o.Id)
            && !db.Invoices.Any(i => i.OrderId == o.Id && i.EmailedAt != null))
            .OrderBy(o => o.Id).Select(o => o.Id).Take(50).ToListAsync(ct);
        foreach (var id in ids)
        {
            ct.ThrowIfCancellationRequested();
            try { await EnqueueAsync(id, ct); }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                log?.LogWarning("Invoice email reconciliation skipped order {OrderId}: reconciliation_failure", id);
            }
        }
    }
}
