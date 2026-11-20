using Microsoft.EntityFrameworkCore;
using OnlineStore.Data;
using OnlineStore.Models;

namespace OnlineStore.Catalog;

public sealed class PricedProduct
{
    public required Product Product { get; init; }
    public decimal Price { get; init; }
    public decimal ListPrice { get; init; }
    public decimal DiscountRate { get; init; }
}

public static class ProductPricing
{
    public static DateTime UtcNow(IServiceProvider services) =>
        (services.GetService<TimeProvider>() ?? TimeProvider.System).GetUtcNow().UtcDateTime;

    public static IQueryable<DiscountProduct> ActiveLinks(StoreDbContext db, DateTime now) =>
        db.DiscountProducts.AsNoTracking().Where(link => link.Discount.CancelledAt == null
            && link.Discount.StartsAt <= now && link.Discount.EndsAt > now);

    // All prices/rates are non-negative. FLOOR(x * 100 + .5) is exactly away-from-zero
    // cents rounding in both .NET and MySQL; no provider-specific ROUND midpoint behavior.
    public static decimal Calculate(decimal listPrice, decimal rate) => Math.Floor(listPrice * (1 - rate) * 100m + .5m) / 100m;

    public static IQueryable<PricedProduct> Query(StoreDbContext db, DateTime now)
    {
        var active = ActiveLinks(db, now);
        return from product in db.Products.AsNoTracking()
               let rate = active.Where(link => link.ProductId == product.Id).Select(link => (decimal?)link.Discount.Rate).Max() ?? 0m
               select new PricedProduct { Product = product, Price = Math.Floor(product.Price * (1 - rate) * 100m + .5m) / 100m,
                   ListPrice = product.Price, DiscountRate = rate };
    }
}
