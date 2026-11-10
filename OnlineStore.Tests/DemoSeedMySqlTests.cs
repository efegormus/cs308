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
                Assert.Equal(4, await db.Orders.CountAsync());
                Assert.Equal(3, await db.Orders.Select(o => o.Status).Distinct().CountAsync());
                Assert.Equal(2, await db.Comments.CountAsync());
                Assert.True(await db.Comments.AnyAsync(c => c.ApprovalStatus == ApprovalStatus.Pending));
                Assert.True(await db.Comments.AnyAsync(c => c.ApprovalStatus == ApprovalStatus.Approved));
                Assert.True(await db.Ratings.AnyAsync());
                Assert.True(await db.Discounts.AnyAsync());
                var product = await db.Products.SingleAsync(p => p.Sku == "PHONE-001");
                product.StockQuantity = 123;
                await db.SaveChangesAsync();
            }
            Assert.Equal(0, await DemoSeedCommand.RunAsync(app));
            await using var check = fixture.Create();
            Assert.Equal(55, await check.Products.CountAsync());
            Assert.Equal(20, await check.Categories.CountAsync());
            Assert.Equal(4, await check.Users.CountAsync());
            Assert.Equal(4, await check.Orders.CountAsync());
            Assert.Equal(4, await check.Deliveries.CountAsync());
            Assert.Equal(4, await check.Invoices.CountAsync());
            Assert.Equal(2, await check.Comments.CountAsync());
            Assert.Equal(1, await check.Refunds.CountAsync());
            Assert.Equal(123, await check.Products.Where(p => p.Sku == "PHONE-001").Select(p => p.StockQuantity).SingleAsync());
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
