using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OnlineStore.Accounts;
using OnlineStore.Data;
using OnlineStore.Invoicing;
using OnlineStore.Models;
using OnlineStore.Purchasing;
using OnlineStore.Seeding;

namespace OnlineStore.Tests;

[Collection("MySql")]
public class DemoSeedMySqlTests(MySqlFixture fixture) : IClassFixture<MySqlFixture>
{
    [MySqlFact]
    public async Task DemoSeed_IsCompleteAndRepeatableWithoutResettingInventory()
    {
        await fixture.ResetAsync();
        var root = Path.Combine(Path.GetTempPath(), "onlinestore-demo-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(root, "OnlineStore"));
        Directory.CreateDirectory(Path.Combine(root, "SeedData"));
        var source = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "SeedData", "seed-products.json"));
        File.Copy(source, Path.Combine(root, "SeedData", "seed-products.json"));
        try
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions {
                EnvironmentName = "Development", ContentRootPath = Path.Combine(root, "OnlineStore") });
            builder.Logging.ClearProviders();
            builder.Services.AddScoped<StoreDbContext>(_ => fixture.Create());
            builder.AddStoreAccounts();
            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.AddScoped<ICheckoutPaymentGateway, MockPaymentGateway>();
            builder.Services.AddScoped<CheckoutService>();
            builder.Services.AddScoped<DeliveryService>();
            builder.Services.AddSingleton<IInvoiceStorage>(new FileInvoiceStorage(Path.Combine(root, "invoices")));
            builder.Services.AddScoped<InvoiceService>();
            builder.Services.AddScoped<InvoiceEmailOutbox>();
            await using var app = builder.Build();
            Assert.Equal(0, await DemoSeedCommand.RunAsync(app));
            await using (var db = fixture.Create())
            {
                Assert.Equal(55, await db.Products.CountAsync());
                Assert.Equal(10, await db.Categories.CountAsync(c => c.ParentId == null));
                Assert.Equal(10, await db.Categories.CountAsync(c => c.ParentId != null));
                Assert.Equal(4, await db.Users.Select(u => u.Role).Distinct().CountAsync());
                Assert.Equal(8, await db.Orders.CountAsync());
                Assert.Equal(3, await db.Orders.Select(o => o.Status).Distinct().CountAsync());
                Assert.Equal(4, await db.Comments.CountAsync());
                Assert.True(await db.Comments.AnyAsync(c => c.ApprovalStatus == ApprovalStatus.Pending));
                Assert.True(await db.Comments.AnyAsync(c => c.ApprovalStatus == ApprovalStatus.Approved));
                Assert.True(await db.Ratings.AnyAsync());
                Assert.True(await db.Discounts.AnyAsync());
                var customers = await db.Users.Where(u => u.Role == UserRole.Customer).ToListAsync();
                Assert.Equal(2, customers.Count);
                foreach (var customer in customers)
                {
                    var deliveredProducts = await db.OrderItems.Where(i => i.UserId == customer.Id
                        && i.Order.Status == OrderStatus.Delivered && i.DeliveredQuantity > 0)
                        .Select(i => i.ProductId).Distinct().CountAsync();
                    Assert.True(deliveredProducts >= 3);
                    Assert.True(await db.OrderItems.AnyAsync(i => i.UserId == customer.Id
                        && i.Order.Status == OrderStatus.Delivered && i.DeliveredQuantity > 0
                        && !db.Ratings.Any(r => r.UserId == customer.Id && r.ProductId == i.ProductId)
                        && !db.Comments.Any(c => c.UserId == customer.Id && c.ProductId == i.ProductId)));
                }
                var phoneId = await db.Products.Where(p => p.Sku == "PHONE-001").Select(p => p.Id).SingleAsync();
                Assert.Equal(4d, await db.Ratings.Where(r => r.ProductId == phoneId).AverageAsync(r => r.Stars));
                Assert.Equal(2, await db.Ratings.CountAsync(r => r.ProductId == phoneId));
                Assert.Equal(2, await db.Comments.CountAsync(c => c.ApprovalStatus == ApprovalStatus.Approved));
                Assert.Equal(2, await db.Comments.CountAsync(c => c.ApprovalStatus == ApprovalStatus.Pending));
                Assert.All(await db.Ratings.Include(r => r.OrderItem).ThenInclude(i => i.Order).ToListAsync(), rating =>
                {
                    Assert.Equal(rating.UserId, rating.OrderItem.UserId);
                    Assert.Equal(rating.ProductId, rating.OrderItem.ProductId);
                    Assert.Equal(OrderStatus.Delivered, rating.OrderItem.Order.Status);
                    Assert.True(rating.OrderItem.DeliveredQuantity > 0);
                });
                var product = await db.Products.SingleAsync(p => p.Sku == "PHONE-001");
                product.StockQuantity = 123;
                var pending = await db.Comments.FirstAsync(c => c.ApprovalStatus == ApprovalStatus.Pending);
                pending.ApprovalStatus = ApprovalStatus.Rejected;
                pending.ModeratedByUserId = await db.Users.Where(u => u.Role == UserRole.ProductManager).Select(u => u.Id).SingleAsync();
                pending.ModeratedAt = DateTime.UtcNow;
                var live = await db.OrderItems.Include(i => i.Order).SingleAsync(i => i.Order.OrderNumber == "DEMO-RATE-LIVE-001");
                db.Ratings.Add(new Rating { UserId = live.UserId, ProductId = live.ProductId, OrderItemId = live.Id, Stars = 2 });
                db.Comments.Add(new Comment { UserId = live.UserId, ProductId = live.ProductId,
                    OrderItemId = live.Id, Body = "Customer-written during the live demo", ApprovalStatus = ApprovalStatus.Pending });
                await db.SaveChangesAsync();
            }
            var credentialFile = Path.Combine(root, ".local", "db-redesign", "demo-accounts.json");
            var originalCredentials = await File.ReadAllTextAsync(credentialFile);
            Assert.Equal(0, await DemoSeedCommand.RunAsync(app));
            Assert.Equal(originalCredentials, await File.ReadAllTextAsync(credentialFile));
            await using var check = fixture.Create();
            Assert.Equal(55, await check.Products.CountAsync());
            Assert.Equal(20, await check.Categories.CountAsync());
            Assert.Equal(5, await check.Users.CountAsync());
            Assert.Equal(8, await check.Orders.CountAsync());
            Assert.Equal(8, await check.Deliveries.CountAsync());
            Assert.Equal(8, await check.Invoices.CountAsync());
            Assert.Equal(5, await check.Comments.CountAsync());
            Assert.Equal(4, await check.Ratings.CountAsync());
            Assert.True(await check.Comments.AnyAsync(c => c.ApprovalStatus == ApprovalStatus.Rejected));
            Assert.True(await check.Comments.AnyAsync(c => c.Body == "Customer-written during the live demo"));
            Assert.Equal(1, await check.Refunds.CountAsync());
            Assert.Equal(123, await check.Products.Where(p => p.Sku == "PHONE-001").Select(p => p.StockQuantity).SingleAsync());
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
