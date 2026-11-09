using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using OnlineStore.Data;
using OnlineStore.Invoicing;
using OnlineStore.Models;

namespace OnlineStore.Tests;

[Collection("MySql")]
public class InvoiceEmailMySqlTests(MySqlFixture fixture) : IClassFixture<MySqlFixture>, IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"invoice-mail-{Guid.NewGuid():N}");
    private readonly TestClock clock = new();
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
    private InvoiceEmailDispatcher Dispatcher(StoreDbContext db, IInvoiceEmailSender sender) =>
        new(db, new InvoiceService(db, new FileInvoiceStorage(root), clock), sender, clock,
            NullLogger<InvoiceEmailDispatcher>.Instance);

    private async Task<long> SeedAsync()
    {
        await fixture.ResetAsync();
        await using var db = fixture.Create();
        var buyer = new User { Name = "Buyer", Email = "current@example.com", TaxId = "TAX", HomeAddress = "Address", PasswordHash = "x" };
        var product = new Product { Sku = "MAIL", Name = "Phone", Price = 10, StockQuantity = 4, Category = new Category { Name = "Phones" } };
        CatalogFixture.CompleteDetails(product);
        db.Users.Add(buyer); db.Products.Add(product);
        await db.SaveChangesAsync();
        var cart = new Cart { UserId = buyer.Id, State = CartState.CheckedOut };
        db.Carts.Add(cart);
        await db.SaveChangesAsync();
        var order = new Order { User = buyer, SourceCartId = cart.Id, Subtotal = 10, GrandTotal = 10, EmailSnapshot = "purchase@example.com", CustomerNameSnapshot = "Buyer",
            TaxIdSnapshot = "TAX", AddressSnapshot = "Address", OrderItems = [OrderItem.Snapshot(product, 1)] };
        order.ConfirmPayment(clock.Now);
        order.Payments.Add(new Payment { UserId = buyer.Id, CartId = cart.Id, CardBrand = "Visa", CardLast4 = "4242", IdempotencyKey = new string('A', 64), Amount = 10, Status = PaymentStatus.Confirmed, ConfirmedAt = clock.Now });
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        await new InvoiceEmailOutbox(db, clock, invoices: new InvoiceService(db, new FileInvoiceStorage(root), clock)).EnqueueAsync(order.Id, default);
        return order.Id;
    }

    [MySqlFact]
    public async Task Delivery_UsesExistingPdfAndSnapshot_AndNeverResendsSuccess()
    {
        var id = await SeedAsync();
        var sink = new LocalInvoiceEmailSender();
        await using var db = fixture.Create();
        var expected = await new InvoiceService(db, new FileInvoiceStorage(root), clock).GetPdfAsync(id, default);
        Assert.True(await Dispatcher(db, sink).DispatchOneAsync(default));
        Assert.False(await Dispatcher(db, sink).DispatchOneAsync(default));
        var mail = Assert.Single(sink.Messages);
        Assert.Equal("purchase@example.com", mail.Recipient);
        Assert.Equal(expected!.FileName, mail.Attachment.FileName);
        Assert.Equal(expected.Content, mail.Attachment.Content);
        var delivery = await db.InvoiceEmailDeliveries.AsNoTracking().SingleAsync();
        Assert.NotNull(delivery.SentAt);
        Assert.Equal(delivery.SentAt, await db.InvoiceEmailDeliveries.Select(x => x.SentAt).SingleAsync());
        Assert.Equal(1, delivery.AttemptCount);
    }

    [MySqlFact]
    public async Task Failure_PersistsSafeRetry_LeavesPurchaseUntouched_ThenSucceeds()
    {
        await SeedAsync();
        var sender = new FailingOnceSender();
        await using var db = fixture.Create();
        Assert.True(await Dispatcher(db, sender).DispatchOneAsync(default));
        var failed = await db.InvoiceEmailDeliveries.AsNoTracking().SingleAsync();
        Assert.Equal(1, failed.AttemptCount);
        Assert.Equal("smtp_transient", failed.LastErrorCode);
        Assert.Null(failed.StoppedAt);
        Assert.Equal("Pending", failed.Status);
        Assert.Equal(clock.Now.AddMinutes(1), failed.NextAttemptAt);
        Assert.Null(failed.SentAt);
        Assert.Null(failed.LeaseUntil);
        Assert.Null(await db.InvoiceEmailDeliveries.Select(x => x.SentAt).SingleAsync());
        Assert.Equal(PaymentStatus.Confirmed, await db.Orders.Select(x => x.PaymentStatus).SingleAsync());
        Assert.Equal(PaymentStatus.Confirmed, await db.Payments.Select(x => x.Status).SingleAsync());
        Assert.Equal(10m, await db.Payments.Select(x => x.Amount).SingleAsync());
        Assert.Equal(4, await db.Products.Select(x => x.StockQuantity).SingleAsync());
        Assert.False(await Dispatcher(db, sender).DispatchOneAsync(default));
        clock.Now = clock.Now.AddMinutes(1);
        Assert.True(await Dispatcher(db, sender).DispatchOneAsync(default));
        Assert.Single(sender.Sink.Messages);
        var sent = await db.InvoiceEmailDeliveries.AsNoTracking().SingleAsync();
        Assert.Equal(2, sent.AttemptCount);
        Assert.NotNull(sent.SentAt);
        Assert.Null(sent.LastErrorCode);
    }

    [MySqlFact]
    public async Task ConcurrentEnqueue_CreatesOneDelivery_AndWorkersSendOnce()
    {
        var id = await SeedAsync();
        await using (var reset = fixture.Create()) await reset.InvoiceEmailDeliveries.ExecuteDeleteAsync();
        await Task.WhenAll(Enumerable.Range(0, 5).Select(async _ =>
        {
            await using var db = fixture.Create();
            await new InvoiceEmailOutbox(db, clock, invoices: new InvoiceService(db, new FileInvoiceStorage(root), clock)).EnqueueAsync(id, default);
        }));
        var sink = new LocalInvoiceEmailSender();
        await Task.WhenAll(Enumerable.Range(0, 5).Select(async _ =>
        {
            await using var db = fixture.Create();
            await Dispatcher(db, sink).DispatchOneAsync(default);
        }));
        Assert.Single(sink.Messages);
        await using var check = fixture.Create();
        Assert.Single(await check.InvoiceEmailDeliveries.ToListAsync());
        Assert.Equal(1, await check.InvoiceEmailDeliveries.Select(x => x.AttemptCount).SingleAsync());
    }

    [MySqlFact]
    public async Task ExpiredLease_RecoversAfterCrash_ActiveLeaseIsSkipped()
    {
        await SeedAsync();
        await using var db = fixture.Create();
        await db.InvoiceEmailDeliveries.ExecuteUpdateAsync(s => s.SetProperty(x => x.LeaseUntil, clock.Now.AddMinutes(5))
            .SetProperty(x => x.LeaseToken, "crashed-worker"));
        var sink = new LocalInvoiceEmailSender();
        Assert.False(await Dispatcher(db, sink).DispatchOneAsync(default));
        clock.Now = clock.Now.AddMinutes(6);
        Assert.True(await Dispatcher(db, sink).DispatchOneAsync(default));
        Assert.Single(sink.Messages);
    }

    [MySqlFact]
    public async Task MissingPdfStorage_RetainsDeliveryForLaterRecovery()
    {
        await SeedAsync();
        await using var db = fixture.Create();
        var sink = new LocalInvoiceEmailSender();
        var dispatcher = new InvoiceEmailDispatcher(db, new InvoiceService(db, new BrokenStorage(), clock), sink, clock,
            NullLogger<InvoiceEmailDispatcher>.Instance);
        Assert.True(await dispatcher.DispatchOneAsync(default));
        Assert.Empty(sink.Messages);
        Assert.Equal("document_or_network_unavailable", await db.InvoiceEmailDeliveries.Select(x => x.LastErrorCode).SingleAsync());
        clock.Now = clock.Now.AddMinutes(1);
        Assert.True(await Dispatcher(db, sink).DispatchOneAsync(default));
        Assert.Single(sink.Messages);
    }

    [MySqlFact]
    public async Task PermanentRecipientAuthenticationConfigurationAndUnexpectedFailures_StopAndAreNeverReclaimed()
    {
        Exception[] errors = [new SmtpFailedRecipientException(SmtpStatusCode.MailboxUnavailable, "private@example.com"),
            new SmtpException((SmtpStatusCode)535), new InvoiceEmailConfigurationException(), new InvalidOperationException("Private detail")];
        string[] codes = ["recipient_rejected", "smtp_authentication", "smtp_configuration", "unexpected_failure"];
        for (var i = 0; i < errors.Length; i++)
        {
            await SeedAsync();
            await using var db = fixture.Create();
            Assert.True(await Dispatcher(db, new ThrowingSender(errors[i])).DispatchOneAsync(default));
            var stopped = await db.InvoiceEmailDeliveries.AsNoTracking().SingleAsync();
            Assert.Equal("Failed", stopped.Status);
            Assert.NotNull(stopped.StoppedAt);
            Assert.Equal(codes[i], stopped.LastErrorCode);
            Assert.Null(stopped.LeaseUntil);
            Assert.Null(stopped.SentAt);
            clock.Now = clock.Now.AddDays(1);
            var sink = new LocalInvoiceEmailSender();
            Assert.False(await Dispatcher(db, sink).DispatchOneAsync(default));
            Assert.Empty(sink.Messages);
            Assert.Equal(1, await db.InvoiceEmailDeliveries.Select(x => x.AttemptCount).SingleAsync());
        }
    }

    [MySqlFact]
    public async Task ShutdownCancellation_DoesNotPersistFailure_AndLeaseCanRecover()
    {
        await SeedAsync();
        using var shutdown = new CancellationTokenSource();
        await using (var db = fixture.Create())
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                Dispatcher(db, new ShutdownSender(shutdown)).DispatchOneAsync(shutdown.Token));
        }
        await using var check = fixture.Create();
        var interrupted = await check.InvoiceEmailDeliveries.AsNoTracking().SingleAsync();
        Assert.Null(interrupted.LastErrorCode);
        Assert.Null(interrupted.StoppedAt);
        Assert.Null(interrupted.SentAt);
        Assert.NotNull(interrupted.LeaseUntil);
        clock.Now = clock.Now.AddMinutes(6);
        var sink = new LocalInvoiceEmailSender();
        Assert.True(await Dispatcher(check, sink).DispatchOneAsync(default));
        Assert.Single(sink.Messages);
    }

    [MySqlFact]
    public async Task PoisonReconciliation_DoesNotBlockSubsequentEnqueueOrBackgroundDispatch()
    {
        var pendingId = await SeedAsync();
        long validId;
        await using (var db = fixture.Create())
        {
            var buyer = await db.Users.SingleAsync();
            var product = await db.Products.SingleAsync();
            Order Historical(string email)
            {
                var order = new Order { UserId = buyer.Id, CustomerNameSnapshot = "Buyer", EmailSnapshot = email,
                    TaxIdSnapshot = "TAX", AddressSnapshot = "Address", OrderItems = [OrderItem.Snapshot(product, 1)] };
                order.ConfirmPayment(clock.Now);
                return order;
            }
            var poison = Historical(""); // Valid old order constraint; invalid new outbox constraint.
            db.Orders.Add(poison);
            await db.SaveChangesAsync();
            var valid = Historical("snapshot@example.com");
            db.Orders.Add(valid);
            await db.SaveChangesAsync();
            validId = valid.Id;
            await new InvoiceEmailOutbox(db, clock, invoices: new InvoiceService(db, new FileInvoiceStorage(root), clock)).ReconcileAsync(default);
            Assert.True(await db.InvoiceEmailDeliveries.AnyAsync(x => x.Invoice!.OrderId == validId));
            Assert.False(await db.InvoiceEmailDeliveries.AnyAsync(x => x.Invoice!.OrderId == poison.Id));
        }
        var sink = new LocalInvoiceEmailSender();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(clock);
        services.AddSingleton<IInvoiceStorage>(new FileInvoiceStorage(root));
        services.AddSingleton<IInvoiceEmailSender>(sink);
        services.AddScoped(_ => fixture.Create());
        services.AddScoped<InvoiceService>();
        services.AddScoped<InvoiceEmailOutbox>();
        services.AddScoped<InvoiceEmailDispatcher>();
        await using var provider = services.BuildServiceProvider();
        using var worker = new InvoiceEmailBackgroundService(provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<InvoiceEmailBackgroundService>.Instance);
        await worker.StartAsync(default);
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (sink.Messages.Count < 2) await Task.Delay(20, deadline.Token);
            Assert.Equal(new[] { pendingId, validId }.Order(), sink.Messages.Select(x => x.OrderId).Order());
        }
        finally { await worker.StopAsync(default); }
    }

    [MySqlFact]
    public async Task ExistingEmailedAt_IsPreservedWithoutEnqueueOrSending()
    {
        var id = await SeedAsync();
        await using var db = fixture.Create();
        await new InvoiceService(db, new FileInvoiceStorage(root), clock).EnsureAsync(id, default);
        await db.InvoiceEmailDeliveries.ExecuteUpdateAsync(s => s.SetProperty(x => x.SentAt, clock.Now).SetProperty(x => x.Status, "Sent"));
        var outbox = new InvoiceEmailOutbox(db, clock, invoices: new InvoiceService(db, new FileInvoiceStorage(root), clock));
        await outbox.EnqueueAsync(id, default);
        await outbox.ReconcileAsync(default);
        var sink = new LocalInvoiceEmailSender();
        Assert.False(await Dispatcher(db, sink).DispatchOneAsync(default));
        Assert.Empty(sink.Messages);
        Assert.Single(await db.InvoiceEmailDeliveries.ToListAsync());
        Assert.Equal(clock.Now, await db.InvoiceEmailDeliveries.Select(x => x.SentAt).SingleAsync());
    }

    private sealed class ThrowingSender(Exception error) : IInvoiceEmailSender
    {
        public Task SendAsync(InvoiceEmailMessage message, CancellationToken ct) => throw error;
    }
    private sealed class ShutdownSender(CancellationTokenSource shutdown) : IInvoiceEmailSender
    {
        public Task SendAsync(InvoiceEmailMessage message, CancellationToken ct)
        {
            shutdown.Cancel();
            return Task.FromCanceled(ct);
        }
    }

    private sealed class TestClock : TimeProvider
    {
        public DateTime Now { get; set; } = new(2026, 11, 6, 10, 0, 0, DateTimeKind.Utc);
        public override DateTimeOffset GetUtcNow() => new(Now);
    }
    private sealed class FailingOnceSender : IInvoiceEmailSender
    {
        public LocalInvoiceEmailSender Sink { get; } = new();
        private bool failed;
        public Task SendAsync(InvoiceEmailMessage message, CancellationToken ct)
        {
            if (!failed) { failed = true; throw new SmtpException(SmtpStatusCode.ServiceNotAvailable, "Secret customer detail must never be persisted"); }
            return Sink.SendAsync(message, ct);
        }
    }
    private sealed class BrokenStorage : IInvoiceStorage
    {
        public Task SaveAsync(string key, byte[] content, CancellationToken ct) => throw new IOException("Unavailable");
        public Task<byte[]?> ReadAsync(string key, CancellationToken ct) => throw new IOException("Unavailable");
    }
}
