using Microsoft.EntityFrameworkCore;
using OnlineStore.Data;
using OnlineStore.Dtos;

namespace OnlineStore.Catalog;

// SCRUM-126: product cards show the public rating. One grouped query for the products of the page, so a list of
// 24 products costs one extra query, not 24. Ratings are the same rows the product page averages (ReviewService.PublicAsync).
public static class ProductRatings
{
    public static async Task<List<ProductListItemDto>> AttachAsync(StoreDbContext db, List<ProductListItemDto> items,
        CancellationToken ct)
    {
        if (items.Count == 0) return items;
        var ids = items.Select(item => item.Id).ToList();
        var summaries = await db.Ratings.AsNoTracking().Where(r => ids.Contains(r.ProductId))
            .GroupBy(r => r.ProductId)
            .Select(group => new { ProductId = group.Key, Count = group.Count(), Average = group.Average(r => (double)r.Stars) })
            .ToDictionaryAsync(summary => summary.ProductId, ct);
        return items.Select(item => summaries.TryGetValue(item.Id, out var summary)
            ? item with { AverageRating = Math.Round(summary.Average, 1), RatingCount = summary.Count }
            : item).ToList();
    }
}
