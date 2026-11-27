using Microsoft.EntityFrameworkCore;
using OnlineStore.Dashboards;
using OnlineStore.Data;
using OnlineStore.Models;
using OnlineStore.Reporting;

namespace OnlineStore.Tests;

// SCRUM-152: daily revenue, cost and profit (service level, in-memory database).
public class ProfitReportServiceTests
{
    private static readonly DateTime Day1 = new(2026, 11, 9, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTimeOffset From = new(Day1);
    private static readonly DateTimeOffset To = new(Day1.AddDays(3));

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static StoreDbContext NewDb() => new(new DbContextOptionsBuilder<StoreDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<User> AddCustomerAsync(StoreDbContext db)
    {
        var user = new User { Name = "Ada", Email = $"{Guid.NewGuid():N}@example.com", PasswordHash = "hash",
            TaxId = "-", HomeAddress = "-", Role = UserRole.Customer };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task<Product> AddProductAsync(StoreDbContext db, decimal price, decimal? cost = null)
    {
        var product = new Product { Sku = "PRF-" + Guid.NewGuid().ToString("N"), Name = "Phone", Price = price, Cost = cost,
            StockQuantity = 50, IsActive = true, Category = new Category { Name = "Profit " + Guid.NewGuid().ToString("N") } };
        CatalogFixture.CompleteDetails(product);
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product;
    }

    // One paid order with one line per (product, quantity, paidPrice), confirmed at the given moment.
    private static async Task<(Order Order, Payment Payment)> PaidOrderAsync(StoreDbContext db, User user, DateTime paidAt,
        params (Product Product, int Quantity, decimal Price)[] lines)
    {
        var total = lines.Sum(l => l.Quantity * l.Price);
        var order = new Order { User = user, UserId = user.Id, CustomerNameSnapshot = user.Name, EmailSnapshot = user.Email,
            TaxIdSnapshot = user.TaxId, AddressSnapshot = user.HomeAddress, Subtotal = total, GrandTotal = total };
        foreach (var l in lines)
        {
            var item = OrderItem.Snapshot(l.Product, l.Quantity, l.Price);
            item.UserId = user.Id;
            order.OrderItems.Add(item);
        }
        order.ConfirmPayment(paidAt);
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        var payment = new Payment { OrderId = order.Id, Order = order, UserId = user.Id, CartId = 1,
            IdempotencyKey = Guid.NewGuid().ToString("N").PadRight(64, '0'), Amount = total, Status = PaymentStatus.Confirmed,
            CreatedAt = paidAt, ConfirmedAt = paidAt, CardBrand = "Visa", CardLast4 = "1234" };
        db.Payments.Add(payment);
        await db.SaveChangesAsync();
        return (order, payment);
    }

    private static async Task<Refund> RefundAsync(StoreDbContext db, Order order, Payment payment, decimal amount,
        DateTime at, string reason = "Return", string status = "Completed", ReturnRequest? request = null)
    {
        var refund = new Refund { OrderId = order.Id, PaymentId = payment.Id, Payment = payment, ReturnRequest = request!,
            ReturnRequestId = request?.Id, IdempotencyKey = Guid.NewGuid().ToString("N").PadRight(64, '0'),
            Amount = amount, Reason = reason, Status = status, CreatedAt = at, RefundedAt = status == "Completed" ? at : null };
        db.Refunds.Add(refund);
        await db.SaveChangesAsync();
        return refund;
    }

    private static Task<ProfitReportResponse> Report(StoreDbContext db) => new ProfitReportService(db).GetAsync(From, To, default);

    [Theory]
    [InlineData(null, 100.00, 50.00)]
    [InlineData(null, 19.99, 10.00)]
    [InlineData(30.00, 100.00, 30.00)]
    [InlineData(0.00, 100.00, 0.00)]
    public void UnitCostOf_UsesTheSnapshotOrTheFiftyPercentDefault(double? cost, double price, double expected) =>
        Assert.Equal((decimal)expected, ProfitReportService.UnitCostOf((decimal?)cost, (decimal)price));

    [Fact]
    public async Task NoData_ReturnsEveryDayWithZeros()
    {
        await using var db = NewDb();

        var report = await Report(db);

        Assert.Equal(3, report.Days.Count);
        Assert.All(report.Days, d => Assert.Equal((0m, 0m, 0m), (d.Revenue, d.Cost, d.Profit)));
        Assert.Equal([new DateOnly(2026, 11, 9), new DateOnly(2026, 11, 10), new DateOnly(2026, 11, 11)], report.Days.Select(d => d.Date));
        Assert.Equal(0m, report.Profit);
    }

    [Fact]
    public async Task CostDefaultsToHalfOfThePaidPrice_WhenThereIsNoSnapshot()
    {
        await using var db = NewDb();
        var user = await AddCustomerAsync(db);
        var product = await AddProductAsync(db, 100m);
        var (order, _) = await PaidOrderAsync(db, user, Day1.AddHours(10), (product, 2, 100m));
        foreach (var item in order.OrderItems) item.UnitCost = null;
        await db.SaveChangesAsync();

        var day = (await Report(db)).Days[0];

        Assert.Equal(200m, day.Revenue);
        Assert.Equal(100m, day.Cost);
        Assert.Equal(100m, day.Profit);
    }

    [Fact]
    public async Task CostUsesTheProductsOwnCostWhenItHasOne()
    {
        await using var db = NewDb();
        var user = await AddCustomerAsync(db);
        var product = await AddProductAsync(db, 100m, cost: 70m);
        await PaidOrderAsync(db, user, Day1.AddHours(10), (product, 3, 100m));

        var day = (await Report(db)).Days[0];

        Assert.Equal(300m, day.Revenue);
        Assert.Equal(210m, day.Cost);
        Assert.Equal(90m, day.Profit);
    }

    [Fact]
    public async Task CostFollowsThePurchaseTimeSnapshot_NotTheCurrentProductCost()
    {
        await using var db = NewDb();
        var user = await AddCustomerAsync(db);
        var product = await AddProductAsync(db, 100m, cost: 40m);
        await PaidOrderAsync(db, user, Day1.AddHours(10), (product, 1, 100m));
        product.Cost = 90m;
        await db.SaveChangesAsync();

        Assert.Equal(40m, (await Report(db)).Days[0].Cost);
    }

    [Fact]
    public async Task EventsAreGroupedByUtcDay()
    {
        await using var db = NewDb();
        var user = await AddCustomerAsync(db);
        var product = await AddProductAsync(db, 100m);
        await PaidOrderAsync(db, user, Day1.AddHours(23).AddMinutes(59), (product, 1, 100m));
        await PaidOrderAsync(db, user, Day1.AddDays(1), (product, 2, 100m));

        var report = await Report(db);

        Assert.Equal([100m, 200m, 0m], report.Days.Select(d => d.Revenue));
        Assert.Equal([50m, 100m, 0m], report.Days.Select(d => d.Cost));
        Assert.Equal(150m, report.Profit);
    }

    [Fact]
    public async Task ThePeriodIsHalfOpen()
    {
        await using var db = NewDb();
        var user = await AddCustomerAsync(db);
        var product = await AddProductAsync(db, 100m);
        await PaidOrderAsync(db, user, From.UtcDateTime, (product, 1, 100m));
        await PaidOrderAsync(db, user, To.UtcDateTime, (product, 1, 100m));
        await PaidOrderAsync(db, user, From.UtcDateTime.AddTicks(-1), (product, 1, 100m));

        var report = await Report(db);

        Assert.Equal(100m, report.Revenue);
        Assert.Equal(50m, report.Cost);
    }

    [Fact]
    public async Task UnconfirmedPaymentsAreIgnored()
    {
        await using var db = NewDb();
        var user = await AddCustomerAsync(db);
        var product = await AddProductAsync(db, 100m);
        var (_, payment) = await PaidOrderAsync(db, user, Day1.AddHours(5), (product, 1, 100m));
        payment.Status = PaymentStatus.Failed;
        await db.SaveChangesAsync();

        var report = await Report(db);

        Assert.Equal(0m, report.Revenue);
        Assert.Equal(0m, report.Cost);
    }

    [Fact]
    public async Task AnOrderWithSeveralLines_CountsThePaymentOnceAndEveryLineCost()
    {
        await using var db = NewDb();
        var user = await AddCustomerAsync(db);
        var a = await AddProductAsync(db, 100m, cost: 60m);
        var b = await AddProductAsync(db, 50m);
        await PaidOrderAsync(db, user, Day1.AddHours(8), (a, 1, 100m), (b, 2, 50m));

        var day = (await Report(db)).Days[0];

        Assert.Equal(200m, day.Revenue);
        Assert.Equal(110m, day.Cost);
        Assert.Equal(90m, day.Profit);
    }

    [Fact]
    public async Task AReturnRefund_LowersRevenueAndGivesBackOnlyTheReturnedGoodsCost()
    {
        await using var db = NewDb();
        var user = await AddCustomerAsync(db);
        var product = await AddProductAsync(db, 100m, cost: 60m);
        var (order, payment) = await PaidOrderAsync(db, user, Day1.AddHours(8), (product, 3, 100m));
        var item = order.OrderItems.Single();
        var request = new ReturnRequest { OrderItemId = item.Id, OrderItem = item, UserId = user.Id, ProductId = product.Id,
            Quantity = 1, Status = ReturnStatus.Approved, RequestedAt = Day1.AddDays(1) };
        db.ReturnRequests.Add(request);
        await db.SaveChangesAsync();
        await RefundAsync(db, order, payment, 100m, Day1.AddDays(1).AddHours(2), request: request);

        var report = await Report(db);

        Assert.Equal(300m, report.Days[0].Revenue);
        Assert.Equal(180m, report.Days[0].Cost);
        Assert.Equal(-100m, report.Days[1].Revenue);
        Assert.Equal(-60m, report.Days[1].Cost);
        Assert.Equal(200m, report.Revenue);
        Assert.Equal(120m, report.Cost);
        Assert.Equal(80m, report.Profit);
    }

    [Fact]
    public async Task ACancellationRefund_GivesBackTheWholeOrdersCost()
    {
        await using var db = NewDb();
        var user = await AddCustomerAsync(db);
        var a = await AddProductAsync(db, 100m, cost: 60m);
        var b = await AddProductAsync(db, 50m);
        var (order, payment) = await PaidOrderAsync(db, user, Day1.AddHours(8), (a, 1, 100m), (b, 2, 50m));
        await RefundAsync(db, order, payment, 200m, Day1.AddDays(1), reason: ReportCancellation());

        var report = await Report(db);

        Assert.Equal(0m, report.Revenue);
        Assert.Equal(0m, report.Cost);
        Assert.Equal(0m, report.Profit);
        Assert.Equal(-110m, report.Days[1].Cost);
    }

    private static string ReportCancellation() => ProfitReportService.CancellationReason;

    [Fact]
    public async Task PendingAndFailedRefundsAreIgnored()
    {
        await using var db = NewDb();
        var user = await AddCustomerAsync(db);
        var product = await AddProductAsync(db, 100m);
        var (pendingOrder, pendingPayment) = await PaidOrderAsync(db, user, Day1.AddHours(8), (product, 1, 100m));
        var (failedOrder, failedPayment) = await PaidOrderAsync(db, user, Day1.AddHours(9), (product, 1, 100m));
        await RefundAsync(db, pendingOrder, pendingPayment, 100m, Day1.AddDays(1), reason: ReportCancellation(), status: "Pending");
        await RefundAsync(db, failedOrder, failedPayment, 100m, Day1.AddDays(1), reason: ReportCancellation(), status: "Failed");

        var report = await Report(db);

        Assert.Equal(200m, report.Revenue);
        Assert.Equal(100m, report.Cost);
    }

    [Fact]
    public async Task TotalsEqualTheSumOfTheDays_AndRevenueMatchesTheSalesDashboard()
    {
        await using var db = NewDb();
        var user = await AddCustomerAsync(db);
        var product = await AddProductAsync(db, 80m, cost: 25m);
        var (order, payment) = await PaidOrderAsync(db, user, Day1.AddHours(3), (product, 2, 80m));
        await PaidOrderAsync(db, user, Day1.AddDays(2).AddHours(3), (product, 1, 80m));
        await RefundAsync(db, order, payment, 30m, Day1.AddDays(1), reason: "Other");

        var report = await Report(db);
        var dashboard = await new DashboardService(db, new FixedClock(To)).GetSalesAsync(From, To, default);

        Assert.Equal(report.Days.Sum(d => d.Revenue), report.Revenue);
        Assert.Equal(report.Days.Sum(d => d.Cost), report.Cost);
        Assert.Equal(report.Revenue - report.Cost, report.Profit);
        Assert.Equal(dashboard.NetRevenue, report.Revenue);
    }

    [Fact]
    public async Task APartialFirstAndLastDay_AreStillListed()
    {
        await using var db = NewDb();

        var report = await new ProfitReportService(db).GetAsync(From.AddHours(18), To.AddHours(-6), default);

        Assert.Equal(3, report.Days.Count);
        Assert.Equal(new DateOnly(2026, 11, 9), report.Days[0].Date);
        Assert.Equal(new DateOnly(2026, 11, 11), report.Days[^1].Date);
    }
}
