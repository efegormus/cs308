using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using OnlineStore.Data;
using OnlineStore.Models;

namespace OnlineStore.Tests;

// Real MySQL checks for the full e-commerce schema (SCRUM-97). They need ONLINESTORE_TEST_MYSQL
// (a local *_tests database), the same as MySqlIntegrationTests, and are skipped otherwise.
[Collection("MySql")]
public class SchemaMySqlTests(MySqlFixture fixture) : IClassFixture<MySqlFixture>
{
    private const int CheckViolated = 3819;
    private const int DuplicateKey = 1062;
    private const int ChildRowExists = 1451;
    private const int DataTooLong = 1406;

    private sealed record Graph(User Customer, User Manager, Product Product, Order Order, OrderItem Line);

    private static User NewUser(string email, UserRole role = UserRole.Customer) => new()
    {
        Name = "Test user", Email = email, PasswordHash = "x",
        TaxId = "TAX-1", HomeAddress = "10 Example Street", Role = role
    };

    // A purchased order with one line, already saved.
    private async Task<Graph> SeedPurchasedOrderAsync()
    {
        await fixture.ResetAsync();
        await using var db = fixture.Create();
        var customer = NewUser("buyer@example.com");
        var manager = NewUser("manager@example.com", UserRole.ProductManager);
        var product = new Product { Sku = "P1", Name = "Phone", Price = 100, StockQuantity = 5, Category = new Category { Name = "Phones" } };
        CatalogFixture.CompleteDetails(product);
        var line = new OrderItem { Product = product, ProductName = "Phone", UnitPrice = 100, UnitCost = 50, Quantity = 1 };
        var order = new Order
        {
            User = customer, OrderItems = [line]
        }.WithCustomerSnapshot(customer);
        order.ConfirmPayment(DateTime.UtcNow);
        db.Users.Add(manager);
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return new Graph(customer, manager, product, order, line);
    }

    // Saves in a fresh context; returns the MySQL error number, or null if MySQL accepted it.
    private async Task<int?> TryAsync(Action<StoreDbContext> add)
    {
        await using var db = fixture.Create();
        add(db);
        try
        {
            await db.SaveChangesAsync();
            return null;
        }
        catch (DbUpdateException error) when (error.InnerException is MySqlException mysql)
        {
            return mysql.Number;
        }
    }

    [MySqlFact]
    public async Task MySql_RejectsRowsThatBreakCheckConstraints()
    {
        var g = await SeedPurchasedOrderAsync();
        var now = DateTime.UtcNow;
        var invalid = new (string Name, Action<StoreDbContext> Add)[]
        {
            ("negative payment", db => db.Payments.Add(new Payment { OrderId = g.Order.Id, IdempotencyKey = "a", Amount = -1 })),
            ("confirmed payment without a time", db => db.Payments.Add(new Payment
                { OrderId = g.Order.Id, IdempotencyKey = "b", Amount = 1, Status = PaymentStatus.Confirmed })),
            ("discount over 100 percent", db => db.Discounts.Add(new Discount { Rate = 1.5m, StartsAt = now, EndsAt = now.AddDays(1) })),
            ("discount that ends before it starts", db => db.Discounts.Add(new Discount { Rate = 0.2m, StartsAt = now, EndsAt = now.AddDays(-1) })),
            ("rating of 6 stars", db => db.Ratings.Add(new Rating
                { UserId = g.Customer.Id, ProductId = g.Product.Id, OrderItemId = g.Line.Id, Stars = 6 })),
            ("approved comment without a moderator", db => db.Comments.Add(new Comment
                { UserId = g.Customer.Id, ProductId = g.Product.Id, OrderItemId = g.Line.Id, Body = "x", ApprovalStatus = ApprovalStatus.Approved })),
            ("cart with two owners", db => db.Carts.Add(new Cart { UserId = g.Customer.Id, GuestTokenHash = "token" })),
            ("active chat without an agent", db => db.Conversations.Add(new Conversation
                { CustomerUserId = g.Customer.Id, Status = ConversationStatus.Active })),
            ("approved return without a reviewer", db => db.ReturnRequests.Add(new ReturnRequest
                { OrderItemId = g.Line.Id, UserId = g.Customer.Id, Quantity = 1, Status = ReturnStatus.Approved, ReceivedAt = now })),
            ("delivered delivery without a time", db => db.Deliveries.Add(new Delivery
                { OrderId = g.Order.Id, AddressSnapshot = "a", Status = OrderStatus.Delivered }))
        };

        foreach (var (name, add) in invalid)
        {
            Assert.True(await TryAsync(add) == CheckViolated, $"{name} should break a check constraint");
        }
    }

    [MySqlFact]
    public async Task MySql_RejectsDuplicatesSoRetriesAreSafe()
    {
        var g = await SeedPurchasedOrderAsync();
        var now = DateTime.UtcNow;

        // The same payment sent twice is stored once.
        Assert.Null(await TryAsync(db => db.Payments.Add(new Payment
            { OrderId = g.Order.Id, IdempotencyKey = "same", Amount = 100, Status = PaymentStatus.Confirmed, ConfirmedAt = now })));
        Assert.Equal(DuplicateKey, await TryAsync(db => db.Payments.Add(new Payment
            { OrderId = g.Order.Id, IdempotencyKey = "same", Amount = 100 })));

        // One invoice per order.
        Invoice NewInvoice(string number) => new()
        {
            OrderId = g.Order.Id, InvoiceNumber = number, CustomerNameSnapshot = "Test user",
            TaxIdSnapshot = "TAX-1", AddressSnapshot = "10 Example Street"
        };
        Assert.Null(await TryAsync(db => db.Invoices.Add(NewInvoice("INV-1"))));
        Assert.Equal(DuplicateKey, await TryAsync(db => db.Invoices.Add(NewInvoice("INV-2"))));

        // One rating per user and product; one guest cart per token.
        Assert.Null(await TryAsync(db => db.Ratings.Add(new Rating
            { UserId = g.Customer.Id, ProductId = g.Product.Id, OrderItemId = g.Line.Id, Stars = 5 })));
        Assert.Equal(DuplicateKey, await TryAsync(db => db.Ratings.Add(new Rating
            { UserId = g.Customer.Id, ProductId = g.Product.Id, OrderItemId = g.Line.Id, Stars = 4 })));
        Assert.Null(await TryAsync(db => db.Carts.Add(new Cart { GuestTokenHash = "guest" })));
        Assert.Equal(DuplicateKey, await TryAsync(db => db.Carts.Add(new Cart { GuestTokenHash = "guest" })));

        // A return request can be refunded only once.
        int returnId;
        int paymentId;
        await using (var db = fixture.Create())
        {
            var request = new ReturnRequest
            {
                OrderItemId = g.Line.Id, UserId = g.Customer.Id, Quantity = 1,
                Status = ReturnStatus.Approved, ReceivedAt = now, ReviewedByUserId = g.Manager.Id
            };
            db.ReturnRequests.Add(request);
            await db.SaveChangesAsync();
            returnId = request.Id;
            paymentId = await db.Payments.Select(p => p.Id).FirstAsync();
        }
        Assert.Null(await TryAsync(db => db.Refunds.Add(new Refund
            { ReturnRequestId = returnId, PaymentId = paymentId, IdempotencyKey = "r1", Amount = 100 })));
        Assert.Equal(DuplicateKey, await TryAsync(db => db.Refunds.Add(new Refund
            { ReturnRequestId = returnId, PaymentId = paymentId, IdempotencyKey = "r2", Amount = 100 })));
    }

    [MySqlFact]
    public async Task MySql_KeepsPaidHistoryWhenParentsAreDeleted()
    {
        var g = await SeedPurchasedOrderAsync();
        await using var db = fixture.Create();
        var now = DateTime.UtcNow;
        var discount = new Discount { Rate = 0.1m, StartsAt = now, EndsAt = now.AddDays(1) };
        db.Discounts.Add(discount);
        db.Payments.Add(new Payment
            { OrderId = g.Order.Id, IdempotencyKey = "p", Amount = 100, Status = PaymentStatus.Confirmed, ConfirmedAt = now });
        await db.SaveChangesAsync();
        await db.OrderItems.Where(item => item.Id == g.Line.Id)
            .ExecuteUpdateAsync(set => set.SetProperty(item => item.DiscountId, discount.Id));

        foreach (var delete in new Func<Task<int>>[]
        {
            () => db.Orders.Where(order => order.Id == g.Order.Id).ExecuteDeleteAsync(),            // has a payment
            () => db.Discounts.Where(existing => existing.Id == discount.Id).ExecuteDeleteAsync(),  // used by an order line
            () => db.Users.Where(user => user.Id == g.Customer.Id).ExecuteDeleteAsync()             // has an order
        })
        {
            Assert.Equal(ChildRowExists, (await Assert.ThrowsAsync<MySqlException>(delete)).Number);
        }
    }

    [MySqlFact]
    public async Task MySql_DeletingACartRemovesItsLinesButKeepsTheChat()
    {
        var g = await SeedPurchasedOrderAsync();
        await using var db = fixture.Create();
        var cart = new Cart { UserId = g.Customer.Id, Items = [new CartItem { ProductId = g.Product.Id, Quantity = 2 }] };
        db.Carts.Add(cart);
        await db.SaveChangesAsync();
        var chat = new Conversation { CustomerUserId = g.Customer.Id, CartId = cart.Id };
        db.Conversations.Add(chat);
        await db.SaveChangesAsync();

        await db.Carts.Where(existing => existing.Id == cart.Id).ExecuteDeleteAsync();

        Assert.Empty(await db.CartItems.AsNoTracking().ToListAsync());
        Assert.Null(await db.Conversations.AsNoTracking().Where(c => c.Id == chat.Id).Select(c => c.CartId).SingleAsync());
    }

    [MySqlFact]
    public async Task MySql_RowVersion_StopsTwoUsersOverwritingEachOther()
    {
        var g = await SeedPurchasedOrderAsync();
        await using var first = fixture.Create();
        await using var second = fixture.Create();
        var seenByFirst = await first.Orders.SingleAsync(order => order.Id == g.Order.Id);
        var seenBySecond = await second.Orders.SingleAsync(order => order.Id == g.Order.Id);

        Assert.True(seenByFirst.TransitionTo(OrderStatus.InTransit, DateTime.UtcNow.AddMinutes(1)));
        await first.SaveChangesAsync();

        // The second user still sees the old version, so their change is refused.
        Assert.True(seenBySecond.TransitionTo(OrderStatus.Cancelled, DateTime.UtcNow.AddMinutes(1)));
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    [MySqlFact]
    public async Task MySql_StoresProfileSizedSnapshotsAndRejectsLongerOnes()
    {
        var g = await SeedPurchasedOrderAsync();
        // Unpurchased orders also store the snapshot columns, so the length limits apply to them too.
        Order NewOrder(int taxLength, int addressLength) => new()
        {
            UserId = g.Customer.Id, CustomerNameSnapshot = "Test user",
            TaxIdSnapshot = new string('t', taxLength), EmailSnapshot = "buyer@example.com",
            AddressSnapshot = new string('a', addressLength)
        };

        Assert.Null(await TryAsync(db => db.Orders.Add(NewOrder(64, 1000))));   // as long as the profile allows
        Assert.Equal(DataTooLong, await TryAsync(db => db.Orders.Add(NewOrder(65, 10))));
        Assert.Equal(DataTooLong, await TryAsync(db => db.Orders.Add(NewOrder(10, 1001))));
    }
}
