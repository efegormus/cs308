using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Net.Mail;
using OnlineStore.Data;
using OnlineStore.Invoicing;
using OnlineStore.Models;

namespace OnlineStore.Tests;

public class InvoiceEmailTests
{
    [Fact]
    public async Task LocalSink_EvictsOldestMessagesAndDoesNotCopyPdfsOnInspection()
    {
        var sink = new LocalInvoiceEmailSender();
        for (var id = 1; id <= LocalInvoiceEmailSender.RetentionLimit + 5; id++)
            await sink.SendAsync(new(id, "local@example.com", new("invoice.pdf", [1, 2, 3])), default);
        Assert.Equal(LocalInvoiceEmailSender.RetentionLimit, sink.Messages.Count);
        Assert.Equal(6, sink.Messages[0].OrderId);
        Assert.Equal(LocalInvoiceEmailSender.RetentionLimit + 5, sink.Messages[^1].OrderId);
        Assert.Same(sink.Messages[0].Attachment.Content, sink.Messages[0].Attachment.Content);
    }

    [Fact]
    public async Task ExistingEmailedAt_PreventsDirectAndReconciledEnqueue()
    {
        await using var db = new StoreDbContext(new DbContextOptionsBuilder<StoreDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var order = new Order { EmailSnapshot = "purchase@example.com" };
        order.ConfirmPayment(DateTime.UtcNow);
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        var emailedAt = DateTime.UtcNow.AddDays(-1);
        db.Invoices.Add(new Invoice { OrderId = order.Id, EmailedAt = emailedAt, InvoiceNumber = "INV-2026-000001", CustomerNameSnapshot = "Buyer", TaxIdSnapshot = "TAX", AddressSnapshot = "Address" });
        await db.SaveChangesAsync();
        var outbox = new InvoiceEmailOutbox(db, TimeProvider.System);
        await outbox.EnqueueAsync(order.Id, default);
        await outbox.ReconcileAsync(default);
        Assert.Empty(await db.InvoiceEmailDeliveries.ToListAsync());
        Assert.Equal(emailedAt, await db.Invoices.Select(x => x.EmailedAt).SingleAsync());
    }

    [Fact]
    public async Task Reconcile_ContinuesAfterPoisonOrderFailsToSave()
    {
        await using var db = new PoisonDb(new DbContextOptionsBuilder<StoreDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var poison = new Order { EmailSnapshot = "" };
        var valid = new Order { EmailSnapshot = "snapshot@example.com" };
        poison.ConfirmPayment(DateTime.UtcNow);
        valid.ConfirmPayment(DateTime.UtcNow);
        db.Orders.AddRange(poison, valid);
        await db.SaveChangesAsync();
        await new InvoiceEmailOutbox(db, TimeProvider.System).ReconcileAsync(default);
        Assert.Equal(valid.Id, (await db.InvoiceEmailDeliveries.SingleAsync()).OrderId);
    }

    private sealed class PoisonDb(DbContextOptions<StoreDbContext> options) : StoreDbContext(options)
    {
        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            if (ChangeTracker.Entries<InvoiceEmailDelivery>().Any(x => x.State == EntityState.Added && x.Entity.RecipientSnapshot == ""))
                throw new DbUpdateException("Simulated recipient constraint violation");
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
    }

    public static IEnumerable<object[]> Failures =>
    [
        [new SmtpFailedRecipientException(SmtpStatusCode.MailboxUnavailable, "private@example.com"), "recipient_rejected", false],
        [new SmtpFailedRecipientException(SmtpStatusCode.MailboxBusy, "private@example.com"), "smtp_transient", true],
        [new SmtpException(SmtpStatusCode.ServiceNotAvailable), "smtp_transient", true],
        [new SmtpException((SmtpStatusCode)535), "smtp_authentication", false],
        [new InvoiceEmailConfigurationException(), "smtp_configuration", false],
        [new InvalidOperationException("Sensitive internal detail"), "unexpected_failure", false],
        [new IOException("Sensitive path"), "document_or_network_unavailable", true]
    ];

    [Theory]
    [MemberData(nameof(Failures))]
    public void Failures_AreClassifiedWithoutExceptionDetails(Exception error, string code, bool retryable) =>
        Assert.Equal(new InvoiceEmailFailure(code, retryable), InvoiceEmailFailure.Classify(error));

    [Fact]
    public async Task Smtp_MissingConfigurationFailsSafelyWithoutNetworkOrCredentials()
    {
        var sender = new SmtpInvoiceEmailSender(new ConfigurationBuilder().Build());
        var error = await Assert.ThrowsAsync<InvoiceEmailConfigurationException>(() =>
            sender.SendAsync(new(1, "snapshot@example.com", new("invoice.pdf", [1])), default));
        Assert.False(InvoiceEmailFailure.Classify(error).Retryable);
    }

    [Fact]
    public async Task LocalSink_PreservesAttachmentFilenameAndStableMessageIdWithoutCredentials()
    {
        var sink = new LocalInvoiceEmailSender();
        byte[] bytes = [37, 80, 68, 70];
        var message = new InvoiceEmailMessage(42, "snapshot@example.com", new InvoiceFile("INV-2026-000042.pdf", bytes));
        await sink.SendAsync(message, default);
        bytes[0] = 0;
        var sent = Assert.Single(sink.Messages);
        Assert.Equal("snapshot@example.com", sent.Recipient);
        Assert.Equal("INV-2026-000042.pdf", sent.Attachment.FileName);
        Assert.Equal(37, sent.Attachment.Content[0]);
        Assert.Equal("<invoice-order-42@onlinestore.invalid>", sent.MessageId);
    }

    [Fact]
    public async Task Enqueue_IsIdempotentAndUsesPurchaseSnapshot()
    {
        await using var db = new StoreDbContext(new DbContextOptionsBuilder<StoreDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var order = new Order { EmailSnapshot = "purchase@example.com", User = new User { Name = "Current", PasswordHash = "x", Email = "current@example.com" } };
        order.ConfirmPayment(DateTime.UtcNow);
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        var outbox = new InvoiceEmailOutbox(db, TimeProvider.System);
        await outbox.EnqueueAsync(order.Id, default);
        await outbox.EnqueueAsync(order.Id, default);
        Assert.Equal("purchase@example.com", (await db.InvoiceEmailDeliveries.SingleAsync()).RecipientSnapshot);
        Assert.Empty(await db.Invoices.ToListAsync()); // Enqueue does not require PDF generation.
    }

    [Fact]
    public async Task Reconcile_RecoversMissingQueueEntryAndIgnoresUnpaidOrders()
    {
        await using var db = new StoreDbContext(new DbContextOptionsBuilder<StoreDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var paid = new Order { EmailSnapshot = "purchase@example.com" };
        paid.ConfirmPayment(DateTime.UtcNow);
        db.Orders.AddRange(paid, new Order { EmailSnapshot = "unpaid@example.com" });
        await db.SaveChangesAsync();
        await new InvoiceEmailOutbox(db, TimeProvider.System).ReconcileAsync(default);
        Assert.Equal(paid.Id, (await db.InvoiceEmailDeliveries.SingleAsync()).OrderId);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(6, 32)]
    [InlineData(7, 60)]
    [InlineData(int.MaxValue, 60)]
    public void RetryDelay_IsExponentialAndCapped(int attempts, int minutes) =>
        Assert.Equal(TimeSpan.FromMinutes(minutes), InvoiceEmailDispatcher.RetryDelay(attempts));
}
