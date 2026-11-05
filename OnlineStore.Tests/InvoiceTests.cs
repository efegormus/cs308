using System.Text;
using Microsoft.EntityFrameworkCore;
using OnlineStore.Data;
using OnlineStore.Invoicing;
using OnlineStore.Models;

namespace OnlineStore.Tests;

// SCRUM-74: invoice contents, storage and idempotent generation without MySQL.
public class InvoiceTests : IDisposable
{
    private readonly string storageRoot = Path.Combine(Path.GetTempPath(), $"invoices-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(storageRoot)) Directory.Delete(storageRoot, recursive: true);
    }

    private static StoreDbContext NewDb() =>
        new(new DbContextOptionsBuilder<StoreDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private InvoiceService Service(StoreDbContext db) => new(db, new FileInvoiceStorage(storageRoot), TimeProvider.System);

    // A paid order whose snapshots differ from the current product and profile on purpose.
    private static async Task<Order> SeedPaidOrderAsync(StoreDbContext db, bool paid = true)
    {
        var user = new User { Name = "Current name", Email = "buyer@example.com", TaxId = "CURRENT-TAX",
            HomeAddress = "Current address", PasswordHash = "x", Role = UserRole.Customer };
        var phone = new Product { Sku = "PHONE", Name = "Phone today", Price = 999, StockQuantity = 5,
            Category = new Category { Name = "Phones" } };
        var tablet = new Product { Sku = "TABLET", Name = "Tablet today", Price = 500, StockQuantity = 5, Category = phone.Category };
        CatalogFixture.CompleteDetails(phone);
        CatalogFixture.CompleteDetails(tablet);
        db.AddRange(user, phone, tablet);
        await db.SaveChangesAsync();

        var order = new Order
        {
            UserId = user.Id, CreatedAt = DateTime.UtcNow,
            CustomerNameSnapshot = "Buyer at purchase", TaxIdSnapshot = "TAX-AT-PURCHASE",
            EmailSnapshot = "buyer@example.com", AddressSnapshot = "Address at purchase"
        };
        // The phone was bought at a 20% discount (80.00 instead of 100.00), two of them.
        var phoneLine = OrderItem.Snapshot(phone, 2, 80m);
        phoneLine.ProductName = "Phone at purchase";
        var tabletLine = OrderItem.Snapshot(tablet, 1, 49.99m);
        tabletLine.ProductName = "Tablet at purchase";
        order.OrderItems.Add(phoneLine);
        order.OrderItems.Add(tabletLine);
        if (paid) order.ConfirmPayment(new DateTime(2026, 11, 5, 9, 30, 0, DateTimeKind.Utc));
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order;
    }

    [Fact]
    public void Render_ProducesPdf()
    {
        var data = new InvoiceData("INV-2026-000001", 1, DateTime.UtcNow, DateTime.UtcNow, "Rümeysa Şahin",
            "1234567890", "Tuzla, İstanbul", "TRY", [new InvoiceLine("Phone", 1, 10m)]);

        var pdf = InvoiceDocument.Render(data);

        Assert.True(pdf.Length > 1000);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [Fact]
    public void Render_RejectsAnInvoiceWithoutLines()
    {
        var data = new InvoiceData("INV-2026-000001", 1, DateTime.UtcNow, DateTime.UtcNow, "A", "B", "C", "TRY", []);
        Assert.Throws<ArgumentException>(() => InvoiceDocument.Render(data));
    }

    [Fact]
    public void Money_UsesTwoDecimalsAndTheCurrency()
    {
        Assert.Equal("1,234.50 TRY", InvoiceDocument.Money(1234.5m, "TRY"));
    }

    [Fact]
    public async Task Ensure_IssuesOneInvoiceWithPaidPricesAndPurchaseSnapshots()
    {
        await using var db = NewDb();
        var order = await SeedPaidOrderAsync(db);

        Assert.True(await Service(db).EnsureAsync(order.Id, default));

        var invoice = await db.Invoices.SingleAsync();
        Assert.Equal($"INV-2026-{order.Id:D6}", invoice.InvoiceNumber);
        Assert.Equal("Buyer at purchase", invoice.CustomerNameSnapshot);
        Assert.Equal("TAX-AT-PURCHASE", invoice.TaxIdSnapshot);
        Assert.Equal("Address at purchase", invoice.AddressSnapshot);
        Assert.Equal($"{invoice.InvoiceNumber}.pdf", invoice.ProtectedDocumentKey);

        var data = InvoiceService.DataFor(order, invoice);
        Assert.Equal(["Phone at purchase", "Tablet at purchase"], data.Lines.Select(line => line.ProductName));
        Assert.Equal(160m, data.Lines[0].Total);   // 2 x 80.00, the discounted price paid
        Assert.Equal(209.99m, data.Total);         // 160.00 + 49.99
        Assert.Equal("TRY", data.Currency);
    }

    [Fact]
    public async Task Ensure_IsIdempotentAndKeepsTheSameDocument()
    {
        await using var db = NewDb();
        var order = await SeedPaidOrderAsync(db);
        var service = Service(db);

        Assert.True(await service.EnsureAsync(order.Id, default));
        var first = await service.GetPdfAsync(order.Id, default);
        Assert.True(await service.EnsureAsync(order.Id, default));
        var second = await service.GetPdfAsync(order.Id, default);

        Assert.Single(await db.Invoices.ToListAsync());
        Assert.Equal(first!.FileName, second!.FileName);
        Assert.Equal(first.Content, second.Content);
    }

    [Fact]
    public async Task Ensure_IgnoresLaterProfileAndProductChanges()
    {
        await using var db = NewDb();
        var order = await SeedPaidOrderAsync(db);
        await Service(db).EnsureAsync(order.Id, default);

        var user = await db.Users.SingleAsync();
        user.Name = "Renamed"; user.TaxId = "NEW-TAX"; user.HomeAddress = "Moved away";
        foreach (var product in await db.Products.ToListAsync()) { product.Price = 1; product.Name = "Renamed product"; }
        await db.SaveChangesAsync();
        await Service(db).EnsureAsync(order.Id, default);

        var invoice = await db.Invoices.SingleAsync();
        Assert.Equal("Buyer at purchase", invoice.CustomerNameSnapshot);
        Assert.Equal("Address at purchase", invoice.AddressSnapshot);
        Assert.Equal(209.99m, InvoiceService.DataFor(order, invoice).Total);
    }

    [Fact]
    public async Task Ensure_RebuildsAMissingFileWithoutASecondInvoice()
    {
        await using var db = NewDb();
        var order = await SeedPaidOrderAsync(db);
        await Service(db).EnsureAsync(order.Id, default);
        var invoice = await db.Invoices.SingleAsync();
        File.Delete(Path.Combine(storageRoot, invoice.ProtectedDocumentKey!));

        var file = await Service(db).GetPdfAsync(order.Id, default);

        Assert.NotNull(file);
        Assert.Single(await db.Invoices.ToListAsync());
    }

    [Fact]
    public async Task Ensure_DoesNotInvoiceUnpaidOrMissingOrders()
    {
        await using var db = NewDb();
        var order = await SeedPaidOrderAsync(db, paid: false);

        Assert.False(await Service(db).EnsureAsync(order.Id, default));
        Assert.False(await Service(db).EnsureAsync(order.Id + 1000, default));
        Assert.Null(await Service(db).GetPdfAsync(order.Id, default));
        Assert.Empty(await db.Invoices.ToListAsync());
    }

    [Theory]
    [InlineData("../secret.pdf")]
    [InlineData("invoice.exe")]
    [InlineData("inv-lowercase.pdf")]
    public async Task Storage_RejectsUnsafeKeys(string key)
    {
        var storage = new FileInvoiceStorage(storageRoot);
        await Assert.ThrowsAsync<ArgumentException>(() => storage.SaveAsync(key, [1], default));
        await Assert.ThrowsAsync<ArgumentException>(() => storage.ReadAsync(key, default));
    }
}
