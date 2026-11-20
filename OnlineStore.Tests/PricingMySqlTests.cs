using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using OnlineStore.Data;
using OnlineStore.Dtos;
using OnlineStore.Models;
using OnlineStore.Catalog;
using OnlineStore.Purchasing;
using OnlineStore.Shopping;

namespace OnlineStore.Tests;

[Collection("MySql")]
public class PricingMySqlTests(MySqlFixture fixture) : IClassFixture<MySqlFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

    [MySqlFact]
    public async Task MySql_EffectivePriceSortFilterCartWishlistAndQuoteAgreeIncludingRounding()
    {
        var g = await SeedAsync(); using var app = new PricingApplication(fixture); using var buyer = app.CreateClient();
        var products = (await buyer.GetFromJsonAsync<List<ProductListItemDto>>("/api/products?sort=price"))!;
        Assert.Equal([g.Tiny.Id, g.BuyerProduct.Id, g.CancelledProduct.Id, g.OtherProduct.Id], products.Select(p => p.Id));
        Assert.Equal([.01m, 50.03m, 60m, 63m], products.Select(p => p.Price));
        var filtered = (await buyer.GetFromJsonAsync<List<ProductListItemDto>>("/api/products?sort=price&minPrice=50&maxPrice=61"))!;
        Assert.Equal([g.BuyerProduct.Id, g.CancelledProduct.Id], filtered.Select(p => p.Id));
        var reversed = (await buyer.GetFromJsonAsync<List<ProductListItemDto>>("/api/products?sort=price&order=desc"))!;
        Assert.Equal(products.Select(p => p.Id).Reverse(), reversed.Select(p => p.Id));
        var detail = (await buyer.GetFromJsonAsync<ProductDetailDto>($"/api/products/{g.BuyerProduct.Id}"))!;
        Assert.Equal(50.03m, detail.Price); Assert.Equal(100.05m, detail.ListPrice); Assert.Equal(.5m, detail.DiscountRate);
        var catalog = (await buyer.GetFromJsonAsync<List<CategoryListing>>("/api/catalog"))!;
        Assert.Equal(50.03m, catalog.SelectMany(c => c.Products).Single(p => p.Id == g.BuyerProduct.Id).Price);
        await Login(buyer, g.Buyer.Email);
        var cart = (await buyer.GetFromJsonAsync<CartDto>("/api/cart"))!;
        Assert.Equal(100.06m, cart.Total); Assert.Equal(50.03m, Assert.Single(cart.Items).UnitPrice);
        Assert.Equal(100.05m, cart.Items[0].ListUnitPrice);
        var wishlist = (await buyer.GetFromJsonAsync<List<WishlistProductDto>>("/api/customer/wishlist"))!;
        Assert.Equal(50.03m, Assert.Single(wishlist).Product.Price);
        var quote = (await buyer.GetFromJsonAsync<CheckoutQuote>("/api/customer/checkout"))!;
        Assert.Equal(cart.Total, quote.Total); Assert.Equal(cart.Items[0].UnitPrice, quote.Items[0].UnitPrice);
        var paged = (await buyer.GetFromJsonAsync<ProductPageDto>("/api/products?sort=price&page=1&pageSize=2"))!;
        Assert.Equal(4, paged.TotalCount); Assert.Equal(products.Take(2).Select(p => p.Id), paged.Items.Select(p => p.Id));
    }

    [MySqlFact]
    public async Task MySql_ChangedCampaignRequiresReviewAndDoesNotTouchStockOrCreatePayment()
    {
        var g = await SeedAsync(); using var app = new PricingApplication(fixture); using var buyer = app.CreateClient();
        await Login(buyer, g.Buyer.Email);
        var quote = (await buyer.GetFromJsonAsync<CheckoutQuote>("/api/customer/checkout"))!;
        await using (var db = fixture.Create())
        {
            var discount = await db.Discounts.SingleAsync(d => d.Name == "Best"); discount.CancelledAt = Now.UtcDateTime;
            await db.SaveChangesAsync();
        }
        var input = new CheckoutRequest(quote.CartId, Guid.NewGuid().ToString("D"), quote.QuoteToken,
            new MockCard { Number = "4242424242424242", SecurityCode = "123", ExpiryMonth = 12, ExpiryYear = 2030 });
        Assert.Equal(HttpStatusCode.Conflict, (await Post(buyer, "/api/customer/checkout", input)).StatusCode);
        var fresh = (await buyer.GetFromJsonAsync<CheckoutQuote>("/api/customer/checkout"))!;
        Assert.Equal(80.04m, fresh.Items[0].UnitPrice); // next best active 20%, never the cancelled 90% campaign
        Assert.NotEqual(quote.QuoteToken, fresh.QuoteToken);
        await using var check = fixture.Create();
        Assert.Equal(20, (await check.Products.SingleAsync(p => p.Id == g.BuyerProduct.Id)).StockQuantity);
        Assert.Single(await check.Orders.ToListAsync()); Assert.Empty(await check.Payments.ToListAsync());
    }

    [MySqlFact]
    public async Task MySql_PaidPriceAndCostSnapshotsSurviveCampaignAndProductChangesAndRetry()
    {
        var g = await SeedAsync(); using var app = new PricingApplication(fixture); using var buyer = app.CreateClient();
        await Login(buyer, g.Buyer.Email); var quote = (await buyer.GetFromJsonAsync<CheckoutQuote>("/api/customer/checkout"))!;
        var input = new CheckoutRequest(quote.CartId, Guid.NewGuid().ToString("D"), quote.QuoteToken,
            new MockCard { Number = "4242424242424242", SecurityCode = "123", ExpiryMonth = 12, ExpiryYear = 2030 });
        var response = await Post(buyer, "/api/customer/checkout", input); Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var receipt = (await response.Content.ReadFromJsonAsync<PurchaseReceipt>())!;
        await using (var db = fixture.Create())
        {
            var product = await db.Products.SingleAsync(p => p.Id == g.BuyerProduct.Id); product.Price = 999; product.Cost = 888;
            foreach (var discount in await db.Discounts.ToListAsync()) discount.CancelledAt = Now.UtcDateTime;
            await db.SaveChangesAsync();
        }
        var retry = await Post(buyer, "/api/customer/checkout", input); Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal(receipt.OrderId, (await retry.Content.ReadFromJsonAsync<PurchaseReceipt>())!.OrderId);
        await using var check = fixture.Create();
        var line = await check.OrderItems.SingleAsync(i => i.OrderId == receipt.OrderId);
        Assert.Equal(50.03m, line.UnitPrice); Assert.Equal(100.05m, line.ListUnitPrice); Assert.Equal(25.02m, line.UnitCost);
        Assert.Single(await check.Payments.ToListAsync()); Assert.Equal(18, (await check.Products.SingleAsync(p => p.Id == g.BuyerProduct.Id)).StockQuantity);
    }

    private async Task<Graph> SeedAsync()
    {
        await fixture.ResetAsync(); await using var db = fixture.Create(); var review = await ReviewFixture.SeedAsync(db);
        var a = review.Product; a.Price = 100.05m;
        Product Product(string sku, decimal price)
        {
            var p = new Product { Sku = sku, Name = sku, Price = price, StockQuantity = 20, CategoryId = a.CategoryId };
            CatalogFixture.CompleteDetails(p); return p;
        }
        var b = Product("OTHER-PRICE", 70); var c = Product("CANCELLED-PRICE", 60); var tiny = Product("TINY-PRICE", .01m);
        db.Products.AddRange(b, c, tiny); await db.SaveChangesAsync();
        void Discount(Product p, string name, decimal rate, int start = -1, int end = 1, bool cancelled = false) =>
            db.DiscountProducts.Add(new DiscountProduct { ProductId = p.Id, Discount = new Discount { Name = name,
                Rate = rate, StartsAt = Now.UtcDateTime.AddHours(start), EndsAt = Now.UtcDateTime.AddHours(end),
                CancelledAt = cancelled ? Now.UtcDateTime.AddMinutes(-1) : null, CreatedByUserId = review.Other.Id } });
        Discount(a, "Best", .5m); Discount(a, "Second", .2m); Discount(a, "Cancelled", .9m, cancelled: true);
        Discount(a, "Expired", .95m, -2, -1); Discount(a, "Future", .99m, 1, 2);
        Discount(b, "Other", .1m); Discount(c, "Only cancelled", .9m, cancelled: true); Discount(tiny, "Tiny", .5m);
        db.Carts.Add(new Cart { UserId = review.Buyer.Id, Items = [new CartItem { ProductId = a.Id, Quantity = 2 }] });
        db.WishlistItems.Add(new WishlistItem { UserId = review.Buyer.Id, ProductId = a.Id });
        await db.SaveChangesAsync(); return new(review.Buyer, a, b, c, tiny);
    }
    private sealed record Graph(User Buyer, Product BuyerProduct, Product OtherProduct, Product CancelledProduct, Product Tiny);
    private static async Task Login(HttpClient client, string email) => Assert.Equal(HttpStatusCode.OK,
        (await Post(client, "/api/auth/login", new { email, password = ReviewFixture.Password })).StatusCode);
    private static async Task<HttpResponseMessage> Post(HttpClient client, string path, object body)
    {
        var token = (await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf")).GetProperty("token").GetString();
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", token); return await client.SendAsync(request);
    }
    private sealed class PricingApplication(MySqlFixture fixture) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development"); builder.UseSetting("MySql:ServerVersion", "8.4.13");
            builder.UseSetting("ConnectionStrings:DefaultConnection", "Server=localhost;Database=unused;User ID=unused;Password=unused");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services => { services.RemoveAll<StoreDbContext>(); services.AddScoped(_ => fixture.Create());
                services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(new FixedClock()); });
        }
    }
    private sealed class FixedClock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
}
