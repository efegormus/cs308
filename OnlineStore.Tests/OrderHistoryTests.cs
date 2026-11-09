using Microsoft.EntityFrameworkCore;
using OnlineStore.Data;
using OnlineStore.Models;
using OnlineStore.Purchasing;

namespace OnlineStore.Tests;

// SCRUM-76: the customer's order history (list, ownership, paging, status) without MySQL.
public class OrderHistoryTests
{
    private static StoreDbContext NewDb() =>
        new(new DbContextOptionsBuilder<StoreDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static User NewUser(string email) => new()
    {
        Name = "Customer " + email, Email = email, TaxId = "TAX", HomeAddress = "Address",
        PasswordHash = "x", Role = UserRole.Customer
    };

    private static Product NewProduct(string sku)
    {
        var product = new Product { Sku = sku, Name = "Product " + sku, Price = 100, StockQuantity = 50,
            Category = new Category { Name = "Category " + sku } };
        CatalogFixture.CompleteDetails(product);
        return product;
    }

    private static readonly DateTime Purchased = new(2026, 11, 5, 9, 30, 0, DateTimeKind.Utc);

    // An order of this customer with the purchase-time snapshots every order needs.
    private static Order NewOrder(User user) => new()
    {
        UserId = user.Id, CustomerNameSnapshot = user.Name, TaxIdSnapshot = user.TaxId,
        EmailSnapshot = user.Email, AddressSnapshot = user.HomeAddress
    };

    // A paid order with one line of `quantity` units at `unitPrice`.
    private static Order PaidOrder(User user, Product product, int quantity = 1, decimal unitPrice = 100m)
    {
        var order = NewOrder(user);
        order.OrderItems.Add(OrderItem.Snapshot(product, quantity, unitPrice));
        order.ConfirmPayment(Purchased);
        return order;
    }

    [Fact]
    public async Task List_ShowsTheCustomersPaidOrdersNewestFirstWithCountsAndTotals()
    {
        await using var db = NewDb();
        var user = NewUser("a@example.com");
        var product = NewProduct("P1");
        db.AddRange(user, product);
        await db.SaveChangesAsync();
        var older = PaidOrder(user, product, quantity: 2, unitPrice: 80m);
        var newer = PaidOrder(user, product, quantity: 1, unitPrice: 49.99m);
        db.Orders.AddRange(older, newer);
        await db.SaveChangesAsync();

        var page = await OrderHistory.ListAsync(db, user.Id, null, default);

        Assert.Equal([newer.Id, older.Id], page.Orders.Select(order => order.OrderId));
        Assert.Null(page.NextBeforeId);
        var oldest = page.Orders[1];
        Assert.Equal(2, oldest.ItemCount);
        Assert.Equal(160m, oldest.Total);
        Assert.Equal("Processing", oldest.Status);
        Assert.Equal("Confirmed", oldest.PaymentStatus);
        Assert.Equal("Pending", oldest.InvoiceStatus);
        Assert.Null(oldest.InvoiceUrl);
    }

    [Fact]
    public async Task List_ShowsProcessingInTransitAndDelivered()
    {
        await using var db = NewDb();
        var user = NewUser("a@example.com");
        var product = NewProduct("P1");
        db.AddRange(user, product);
        await db.SaveChangesAsync();
        var processing = PaidOrder(user, product);
        var inTransit = PaidOrder(user, product);
        Assert.True(inTransit.TransitionTo(OrderStatus.InTransit, Purchased.AddDays(1)));
        var delivered = PaidOrder(user, product);
        Assert.True(delivered.TransitionTo(OrderStatus.InTransit, Purchased.AddDays(1)));
        Assert.True(delivered.TransitionTo(OrderStatus.Delivered, Purchased.AddDays(2)));
        db.Orders.AddRange(processing, inTransit, delivered);
        await db.SaveChangesAsync();

        var page = await OrderHistory.ListAsync(db, user.Id, null, default);

        Assert.Equal(["Delivered", "InTransit", "Processing"], page.Orders.Select(order => order.Status));
        Assert.NotNull(page.Orders[0].DeliveredAt);
        Assert.Null(page.Orders[1].DeliveredAt);
    }

    [Fact]
    public async Task List_NeverShowsAnotherCustomersOrdersOrUnpaidOnes()
    {
        await using var db = NewDb();
        var me = NewUser("me@example.com");
        var other = NewUser("other@example.com");
        var product = NewProduct("P1");
        db.AddRange(me, other, product);
        await db.SaveChangesAsync();
        var mine = PaidOrder(me, product);
        var theirs = PaidOrder(other, product);
        var unpaid = NewOrder(me);
        unpaid.OrderItems.Add(OrderItem.Snapshot(product, 1, 100m));
        db.Orders.AddRange(mine, theirs, unpaid);
        await db.SaveChangesAsync();

        var page = await OrderHistory.ListAsync(db, me.Id, null, default);

        Assert.Equal([mine.Id], page.Orders.Select(order => order.OrderId));
        Assert.Empty((await OrderHistory.ListAsync(db, other.Id + 1000, null, default)).Orders);
    }

    [Fact]
    public async Task List_PagesWithBeforeIdUntilTheLastPage()
    {
        await using var db = NewDb();
        var user = NewUser("a@example.com");
        var product = NewProduct("P1");
        db.AddRange(user, product);
        await db.SaveChangesAsync();
        var orders = Enumerable.Range(0, 5).Select(_ => PaidOrder(user, product)).ToList();
        db.Orders.AddRange(orders);
        await db.SaveChangesAsync();
        var newestFirst = orders.OrderByDescending(order => order.Id).Select(order => order.Id).ToList();

        var first = await OrderHistory.ListAsync(db, user.Id, null, default, pageSize: 2);
        Assert.Equal(newestFirst.Take(2), first.Orders.Select(order => order.OrderId));
        Assert.Equal(newestFirst[1], first.NextBeforeId);

        var second = await OrderHistory.ListAsync(db, user.Id, first.NextBeforeId, default, pageSize: 2);
        Assert.Equal(newestFirst.Skip(2).Take(2), second.Orders.Select(order => order.OrderId));

        var last = await OrderHistory.ListAsync(db, user.Id, second.NextBeforeId, default, pageSize: 2);
        Assert.Equal(newestFirst.Skip(4), last.Orders.Select(order => order.OrderId));
        Assert.Null(last.NextBeforeId);
    }

    [Fact]
    public async Task List_ReportsAReadyInvoiceWithItsDownloadLink()
    {
        await using var db = NewDb();
        var user = NewUser("a@example.com");
        var product = NewProduct("P1");
        db.AddRange(user, product);
        await db.SaveChangesAsync();
        var order = PaidOrder(user, product);
        order.Invoice = new Invoice
        {
            InvoiceNumber = "INV-2026-000001", CustomerNameSnapshot = "N", TaxIdSnapshot = "T",
            AddressSnapshot = "A", ProtectedDocumentKey = "INV-2026-000001.pdf"
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var summary = (await OrderHistory.ListAsync(db, user.Id, null, default)).Orders.Single();

        Assert.Equal("Ready", summary.InvoiceStatus);
        Assert.Equal($"/api/customer/orders/{order.Id}/invoice", summary.InvoiceUrl);
    }
}
