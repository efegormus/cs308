using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OnlineStore.Accounts;
using OnlineStore.Data;
using OnlineStore.Invoicing;
using OnlineStore.Models;
using OnlineStore.Purchasing;

namespace OnlineStore.Seeding;

// Development fixtures built through the same account/checkout/delivery services as the UI.
public static class DemoSeedCommand
{
    public const string Flag = "--seed-demo";
    public static async Task<int> RunAsync(WebApplication app)
    {
        if (!app.Environment.IsDevelopment()) throw new InvalidOperationException("Demo seeding requires Development.");
        var result = await SeedCommand.RunAsync(app);
        if (result != 0) return result;
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StoreDbContext>();
        var accounts = scope.ServiceProvider.GetRequiredService<AccountService>();
        var checkout = scope.ServiceProvider.GetRequiredService<CheckoutService>();
        var deliveryService = scope.ServiceProvider.GetRequiredService<DeliveryService>();
        var invoices = scope.ServiceProvider.GetRequiredService<InvoiceService>();
        var outbox = scope.ServiceProvider.GetRequiredService<InvoiceEmailOutbox>();
        var directory = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", ".local", "db-redesign"));
        Directory.CreateDirectory(directory);
        var credentialFile = Path.Combine(directory, "demo-accounts.json");
        Dictionary<string, string> credentials;
        if (File.Exists(credentialFile)) credentials = JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync(credentialFile))!;
        else
        {
            credentials = Enum.GetNames<UserRole>().ToDictionary(role => role.ToLowerInvariant() + "@onlinestore.invalid",
                _ => Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)) + "Aa1!");
            await File.WriteAllTextAsync(credentialFile, JsonSerializer.Serialize(credentials, new JsonSerializerOptions { WriteIndented = true }));
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(credentialFile, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        var users = new Dictionary<UserRole, User>();
        foreach (var role in Enum.GetValues<UserRole>())
        {
            var email = role.ToString().ToLowerInvariant() + "@onlinestore.invalid";
            var index = db.Protector.EmailIndex(email);
            var user = await db.Users.SingleOrDefaultAsync(u => u.EmailBlindIndex == index);
            if (user is null)
            {
                user = await accounts.CreateCustomerAsync(new("Demo " + role, email, "DEMO-TAX-" + role, "Demo address, Istanbul"), credentials[email])
                    ?? throw new InvalidOperationException("Cannot create demo account.");
                await accounts.SetRoleAsync(email, role);
            }
            users[role] = user;
        }
        var customer = users[UserRole.Customer];
        var productManager = users[UserRole.ProductManager];
        var salesManager = users[UserRole.SalesManager];
        var support = users[UserRole.SupportAgent];
        var phone = await db.Products.SingleAsync(p => p.Sku == "PHONE-001");
        if (!await db.Discounts.AnyAsync(d => d.Name == "Demo electronics discount"))
        {
            db.Discounts.Add(new Discount { Name = "Demo electronics discount", CreatedByUserId = salesManager.Id,
                Rate = .10m, StartsAt = DateTime.UtcNow.AddDays(-1), EndsAt = DateTime.UtcNow.AddDays(30),
                Products = [new DiscountProduct { ProductId = phone.Id }] });
            await db.SaveChangesAsync();
        }

        async Task<Order> Purchase(string marker, string sku, int quantity, OrderStatus target)
        {
            var existing = await db.Orders.Include(o => o.OrderItems).SingleOrDefaultAsync(o => o.OrderNumber == marker);
            if (existing is not null) return existing;
            var product = await db.Products.SingleAsync(p => p.Sku == sku);
            // An interrupted previous fixture run can reuse its open cart.
            var cart = await db.Carts.Include(c => c.Items).SingleOrDefaultAsync(c => c.UserId == customer.Id && c.State == CartState.Open);
            if (cart is null) { cart = new Cart { UserId = customer.Id }; db.Carts.Add(cart); }
            db.CartItems.RemoveRange(cart.Items);
            cart.Items = [new CartItem { ProductId = product.Id, Quantity = quantity }];
            await db.SaveChangesAsync();
            var quote = await checkout.QuoteAsync(customer.Id, default);
            var receipt = await checkout.CompleteAsync(customer.Id, new(quote.CartId, Guid.NewGuid().ToString("D"), quote.QuoteToken,
                new MockCard { Number = "4242424242424242", ExpiryMonth = 12, ExpiryYear = DateTime.UtcNow.Year + 1, SecurityCode = "123" }), default);
            var order = await db.Orders.Include(o => o.OrderItems).SingleAsync(o => o.Id == receipt.Receipt.OrderId);
            order.OrderNumber = marker;
            await db.SaveChangesAsync();
            if (target is OrderStatus.InTransit or OrderStatus.Delivered)
            {
                var view = (await deliveryService.ListAsync(0, default)).Items.Single(d => d.OrderId == order.Id);
                view = await deliveryService.TransitionAsync(view.Id, new("InTransit", view.Version), default);
                if (target == OrderStatus.Delivered) await deliveryService.TransitionAsync(view.Id, new("Delivered", view.Version), default);
            }
            await invoices.EnsureAsync(order.Id, default);
            await outbox.EnqueueAsync(order.Id, default);
            return order;
        }

        var delivered = await Purchase("DEMO-DELIVERED-001", "PHONE-001", 2, OrderStatus.Delivered);
        await Purchase("DEMO-PROCESSING-001", "LAPTOPS-001", 1, OrderStatus.Processing);
        await Purchase("DEMO-INTRANSIT-001", "AUDIO-001", 1, OrderStatus.InTransit);
        var line = delivered.OrderItems.Single();
        if (!await db.Ratings.AnyAsync(r => r.UserId == customer.Id && r.ProductId == phone.Id))
        {
            db.Ratings.Add(new Rating { UserId = customer.Id, ProductId = phone.Id, OrderItemId = line.Id, Stars = 5 });
            db.Comments.Add(new Comment { UserId = customer.Id, ProductId = phone.Id, OrderItemId = line.Id,
                Body = "Demo: product arrived and works well.", ApprovalStatus = ApprovalStatus.Approved,
                ModeratedByUserId = productManager.Id, ModeratedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        if (!await db.ReturnRequests.AnyAsync(r => r.OrderItemId == line.Id))
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            var now = DateTime.UtcNow;
            line.ReturnRequestedQuantity = 1;
            line.RefundedQuantity = 1;
            var request = new ReturnRequest { OrderItemId = line.Id, UserId = customer.Id, ProductId = phone.Id,
                Quantity = 1, Reason = "Demo selective return", Status = ReturnStatus.Approved,
                ReviewedByUserId = salesManager.Id, ReviewedAt = now, ReceivedAt = now };
            var payment = await db.Payments.SingleAsync(p => p.OrderId == delivered.Id);
            request.Refund = new Refund { OrderId = delivered.Id, PaymentId = payment.Id, Reason = "Return",
                IdempotencyKey = Convert.ToHexString(SHA256.HashData(Guid.NewGuid().ToByteArray())),
                Amount = line.UnitPrice, Status = "Completed", RefundedAt = now };
            db.ReturnRequests.Add(request);
            await db.Products.Where(p => p.Id == phone.Id).ExecuteUpdateAsync(s => s.SetProperty(p => p.StockQuantity, p => p.StockQuantity + 1));
            db.Notifications.Add(new Notification { UserId = customer.Id, Kind = NotificationKind.RefundApproved,
                Title = "Demo refund approved", Body = $"Demo refund: {line.UnitPrice:0.00} TRY", DeduplicationKey = "demo:refund:1" });
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        if (!await db.WishlistItems.AnyAsync(w => w.UserId == customer.Id && w.ProductId == phone.Id))
        {
            db.WishlistItems.Add(new WishlistItem { UserId = customer.Id, ProductId = phone.Id });
            await db.SaveChangesAsync();
        }
        if (!await db.Conversations.AnyAsync(c => c.CustomerUserId == customer.Id))
        {
            var pdf = (await invoices.GetPdfAsync(delivered.Id, default))!;
            var attachmentPath = Path.Combine(directory, "demo-support.pdf");
            await File.WriteAllBytesAsync(attachmentPath, pdf.Content);
            db.Conversations.Add(new Conversation { CustomerUserId = customer.Id, ClaimedByUserId = support.Id,
                ClaimedAt = DateTime.UtcNow, Status = ConversationStatus.Active,
                Messages = [new ChatMessage { SenderUserId = customer.Id, SenderKind = "Customer", Body = "Demo: a question about my invoice.",
                    Attachments = [new ChatAttachment { ProtectedStorageKey = attachmentPath, OriginalFileName = "demo-invoice.pdf",
                        ContentType = "application/pdf", SizeBytes = pdf.Content.Length, Sha256 = SHA256.HashData(pdf.Content) }] },
                    new ChatMessage { SenderUserId = support.Id, SenderKind = "Agent", Body = "Demo: I can help with that." }] });
            await db.SaveChangesAsync();
        }
        if (!await db.Carts.AnyAsync(c => c.UserId == customer.Id && c.State == CartState.Open))
        {
            db.Carts.Add(new Cart { UserId = customer.Id, Items = [new CartItem { ProductId = phone.Id, Quantity = 1 }] });
            await db.SaveChangesAsync();
        }
        Console.WriteLine("Demo fixtures ready. Private login credentials: .local/db-redesign/demo-accounts.json");
        return 0;
    }
}
