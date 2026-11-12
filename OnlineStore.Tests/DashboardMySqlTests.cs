using Microsoft.EntityFrameworkCore;
using OnlineStore.Dashboards;
using OnlineStore.Models;

namespace OnlineStore.Tests;

[Collection("MySql")]
public sealed class DashboardMySqlTests(MySqlFixture fixture) : IClassFixture<MySqlFixture>
{
    [MySqlFact]
    public async Task DashboardService_ReturnsContractSummariesFromMigratedMySqlSchema()
    {
        await fixture.ResetAsync();
        await using var db = fixture.Create();

        var customer = new User
        {
            Name = "Dashboard Customer", Email = "dashboard-customer@example.com",
            PasswordHash = "test-hash", TaxId = "TAX-DASHBOARD", HomeAddress = "10 Example Street"
        };
        var manager = new User
        {
            Name = "Dashboard Manager", Email = "dashboard-manager@example.com",
            PasswordHash = "test-hash", Role = UserRole.SalesManager
        };
        var lowStock = NewProduct("DASHBOARD-LOW", 40m, 5);
        var regularStock = NewProduct("DASHBOARD-REGULAR", 80m, 6);
        db.Users.AddRange(customer, manager);
        db.Products.AddRange(lowStock, regularStock);
        await db.SaveChangesAsync();

        var cart = new Cart { UserId = customer.Id };
        db.Carts.Add(cart);
        await db.SaveChangesAsync();

        var order = new Order
        {
            UserId = customer.Id,
            User = customer,
            CustomerNameSnapshot = customer.Name,
            TaxIdSnapshot = customer.TaxId,
            EmailSnapshot = customer.Email,
            AddressSnapshot = customer.HomeAddress,
            Subtotal = 120m,
            GrandTotal = 120m,
            OrderItems =
            [
                OrderItem.Snapshot(lowStock, 1, 40m),
                OrderItem.Snapshot(regularStock, 1, 80m)
            ]
        };
        Assert.True(order.ConfirmPayment(new DateTime(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc)));
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var payment = new Payment
        {
            OrderId = order.Id,
            Order = order,
            UserId = customer.Id,
            CartId = cart.Id,
            IdempotencyKey = new string('A', 64),
            Amount = 120m,
            Status = PaymentStatus.Confirmed,
            CreatedAt = new DateTime(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc),
            ConfirmedAt = new DateTime(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc),
            CardBrand = "Visa",
            CardLast4 = "4242"
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        db.Refunds.Add(new Refund
        {
            OrderId = order.Id,
            PaymentId = payment.Id,
            Payment = payment,
            IdempotencyKey = new string('B', 64),
            Amount = 20m,
            Reason = "Cancellation",
            Status = "Completed",
            CreatedAt = new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc),
            RefundedAt = new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc)
        });
        db.Invoices.Add(new Invoice
        {
            OrderId = order.Id,
            Order = order,
            InvoiceNumber = "INV-DASHBOARD-1",
            CustomerNameSnapshot = customer.Name,
            TaxIdSnapshot = customer.TaxId,
            AddressSnapshot = customer.HomeAddress,
            ProtectedDocumentKey = "dashboard-test.pdf",
            IssuedAt = new DateTime(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc),
            Subtotal = 120m,
            GrandTotal = 120m,
            PdfSha256 = new byte[32]
        });
        var now = DateTime.UtcNow;
        db.Discounts.Add(new Discount
        {
            Name = "Dashboard active",
            CreatedByUserId = manager.Id,
            Rate = 0.1m,
            StartsAt = now.AddDays(-1),
            EndsAt = now.AddDays(1),
            Products = [new DiscountProduct { ProductId = lowStock.Id, Product = lowStock }]
        });
        Assert.True(order.TransitionTo(OrderStatus.InTransit,
            new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc)));
        db.Deliveries.Add(new Delivery
        {
            OrderId = order.Id,
            UserId = customer.Id,
            Order = order,
            AddressSnapshot = "10 Example Street",
            Status = OrderStatus.InTransit,
            CreatedAt = new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc)
        });
        db.Comments.Add(new Comment
        {
            UserId = customer.Id,
            ProductId = lowStock.Id,
            OrderItemId = order.OrderItems.First().Id,
            Product = lowStock,
            OrderItem = order.OrderItems.First(),
            Body = "Pending dashboard comment",
            ApprovalStatus = ApprovalStatus.Pending,
            CreatedAt = new DateTime(2026, 11, 10, 13, 0, 0, DateTimeKind.Utc)
        });
        db.WishlistItems.Add(new WishlistItem { UserId = customer.Id, ProductId = lowStock.Id });
        await db.SaveChangesAsync();

        var dashboards = new DashboardService(db, new FixedTimeProvider(new DateTimeOffset(now)));
        var customerDashboard = await dashboards.GetCustomerAsync(customer.Id, CancellationToken.None);
        var salesDashboard = await dashboards.GetSalesAsync(
            new DateTimeOffset(2026, 11, 9, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 11, 11, 0, 0, 0, TimeSpan.Zero),
            CancellationToken.None);
        var productDashboard = await dashboards.GetProductManagementAsync(CancellationToken.None);
        var users = await dashboards.ListUsersAsync(1, 20, "Dashboard", CancellationToken.None);

        Assert.NotNull(customerDashboard);
        Assert.Equal(customer.Id, customerDashboard.Profile.Id);
        Assert.Equal(1, customerDashboard.WishlistCount);
        Assert.Equal(order.Id, Assert.Single(customerDashboard.RecentConfirmedOrders).OrderId);
        Assert.Equal(120m, customerDashboard.RecentConfirmedOrders[0].Total);
        Assert.Equal(120m, salesDashboard.GrossRevenue);
        Assert.Equal(20m, salesDashboard.Refunds);
        Assert.Equal(100m, salesDashboard.NetRevenue);
        Assert.Equal("INV-DASHBOARD-1", Assert.Single(salesDashboard.RecentInvoices).InvoiceNumber);
        Assert.Equal("Dashboard active", Assert.Single(salesDashboard.ActiveDiscounts).Name);
        Assert.Equal("DASHBOARD-LOW", Assert.Single(productDashboard.LowStockProducts).Sku);
        Assert.Equal("InTransit", Assert.Single(productDashboard.PendingDeliveries).Status);
        Assert.Equal("Pending dashboard comment",
            Assert.Single(productDashboard.CommentsWaitingForApproval).Body);
        Assert.Equal(new[] { "Dashboard Customer", "Dashboard Manager" }, users.Users.Select(user => user.Name));
    }

    private static Product NewProduct(string sku, decimal price, int stock)
    {
        var product = new Product
        {
            Sku = sku,
            Name = sku + " product",
            Price = price,
            StockQuantity = stock,
            Category = new Category { Name = sku + " category", Slug = sku.ToLowerInvariant() }
        };
        CatalogFixture.CompleteDetails(product);
        return product;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
