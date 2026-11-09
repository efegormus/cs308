using Microsoft.EntityFrameworkCore;
using OnlineStore.Data;

namespace OnlineStore.Invoicing;

public sealed class InvoiceEmailDispatcher(StoreDbContext db, InvoiceService invoices, IInvoiceEmailSender sender,
    TimeProvider clock, ILogger<InvoiceEmailDispatcher> log)
{
    public static TimeSpan RetryDelay(int attempts) => TimeSpan.FromMinutes(Math.Min(60, Math.Pow(2, Math.Min(10, Math.Max(0, attempts - 1)))));

    public async Task<bool> DispatchOneAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var id = await db.InvoiceEmailDeliveries.AsNoTracking()
            .Where(x => x.Kind == "InvoiceEmail" && x.SentAt == null && x.Status != "Failed" && x.NextAttemptAt <= now && (x.LeaseUntil == null || x.LeaseUntil <= now))
            .OrderBy(x => x.NextAttemptAt).Select(x => (long?)x.Id).FirstOrDefaultAsync(ct);
        if (id is null) return false;
        var token = Guid.NewGuid().ToString("N");
        var lease = now.AddMinutes(5);
        var claimed = await db.InvoiceEmailDeliveries.Where(x => x.Id == id && x.SentAt == null && x.Status != "Failed"
                && x.NextAttemptAt <= now && (x.LeaseUntil == null || x.LeaseUntil <= now))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.LeaseUntil, lease).SetProperty(x => x.LeaseToken, token)
                .SetProperty(x => x.Status, "Sending").SetProperty(x => x.LastAttemptAt, now).SetProperty(x => x.AttemptCount, x => x.AttemptCount + 1), ct);
        if (claimed == 0) return false;

        // Hold a row lock across sending: even if a slow send outlives its lease, a second claimant
        // cannot send concurrently. The token fences off a claimant delayed before acquiring this lock.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var locked = await db.InvoiceEmailDeliveries.FromSqlInterpolated(
            $"SELECT * FROM EmailOutbox WHERE Id = {id.Value} FOR UPDATE").AsNoTracking().ToListAsync(ct);
        var delivery = locked.Single();
        if (delivery.SentAt != null || delivery.Status == "Failed" || delivery.LeaseToken != token) return false;
        var orderId = await db.Invoices.Where(i => i.Id == delivery.InvoiceId).Select(i => i.OrderId).SingleAsync(ct);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromMinutes(1));
        try
        {
            var pdf = await invoices.GetPdfAsync(orderId, timeout.Token)
                ?? throw new InvalidOperationException("Invoice unavailable.");
            await sender.SendAsync(new InvoiceEmailMessage(orderId, delivery.RecipientSnapshot, pdf), timeout.Token);
            ct.ThrowIfCancellationRequested();
        }
        catch (Exception e) when (!ct.IsCancellationRequested)
        {
            // Never persist exception messages or log exception objects (SMTP may include addresses).
            var failure = InvoiceEmailFailure.Classify(e);
            var stopped = failure.Retryable ? (DateTime?)null : clock.GetUtcNow().UtcDateTime;
            var next = clock.GetUtcNow().UtcDateTime.Add(RetryDelay(delivery.AttemptCount));
            await db.InvoiceEmailDeliveries.Where(x => x.Id == id && x.LeaseToken == token)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.NextAttemptAt, next).SetProperty(x => x.LeaseUntil, (DateTime?)null)
                    .SetProperty(x => x.LeaseToken, (string?)null).SetProperty(x => x.LastErrorCode, failure.Code)
                    .SetProperty(x => x.Status, failure.Retryable ? "Pending" : "Failed"), ct);
            log.LogWarning("Invoice email order {OrderId} attempt {Attempt}: {ErrorCode}", id, delivery.AttemptCount, failure.Code);
            await transaction.CommitAsync(ct);
            return true;
        }
        // Database completion errors roll back both success markers and leave the claim recoverable.
        var sent = clock.GetUtcNow().UtcDateTime;
        await db.InvoiceEmailDeliveries.Where(x => x.Id == id && x.LeaseToken == token)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "Sent").SetProperty(x => x.SentAt, sent).SetProperty(x => x.LeaseUntil, (DateTime?)null)
                .SetProperty(x => x.LeaseToken, (string?)null).SetProperty(x => x.LastErrorCode, (string?)null), ct);
        await transaction.CommitAsync(ct);
        return true;
    }
}
