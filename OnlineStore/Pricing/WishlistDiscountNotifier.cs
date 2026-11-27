using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OnlineStore.Data;
using OnlineStore.Models;

namespace OnlineStore.Pricing;

// SCRUM-85: when a discount is created, every active customer who has a discounted product on their wishlist
// gets an in-app Notification per product and one email per customer listing those products.
// The DeduplicationKey (discount, product, customer) makes a repeated run create and send nothing twice.
public sealed class WishlistDiscountNotifier(StoreDbContext db, IDiscountEmailSender sender,
    ILogger<WishlistDiscountNotifier>? log = null)
{
    public const string Title = "A product on your wishlist is on sale";
    private const int MaxListedProducts = 10;

    public static string Key(long discountId, long productId, long userId) => $"wishlist-discount:{discountId}:{productId}:{userId}";

    // Returns how many customers were emailed. Mail problems are logged and never fail the discount itself.
    public async Task<int> NotifyAsync(long discountId, CancellationToken ct)
    {
        var discount = await db.Discounts.AsNoTracking().Include(d => d.Products)
            .SingleOrDefaultAsync(d => d.Id == discountId, ct);
        if (discount is null) return 0;
        var productIds = discount.Products.Select(p => p.ProductId).ToList();

        var wishes = await db.WishlistItems.AsNoTracking()
            .Where(w => productIds.Contains(w.ProductId) && db.Users.Any(u => u.Id == w.UserId && u.IsActive))
            .Select(w => new { w.UserId, w.ProductId, ProductName = w.Product.Name })
            .ToListAsync(ct);
        if (wishes.Count == 0) return 0;

        var keys = wishes.Select(w => Key(discountId, w.ProductId, w.UserId)).ToList();
        var existing = (await db.Notifications.AsNoTracking().Where(n => keys.Contains(n.DeduplicationKey))
            .Select(n => n.DeduplicationKey).ToListAsync(ct)).ToHashSet();
        var fresh = wishes.Where(w => !existing.Contains(Key(discountId, w.ProductId, w.UserId))).ToList();
        if (fresh.Count == 0) return 0;

        var percent = (discount.Rate * 100).ToString("0.##", CultureInfo.InvariantCulture);
        var period = $"{discount.StartsAt:yyyy-MM-dd} to {discount.EndsAt:yyyy-MM-dd} (UTC)";
        db.Notifications.AddRange(fresh.Select(w => new Notification
        {
            UserId = w.UserId, ProductId = w.ProductId, DiscountId = discountId, Kind = NotificationKind.WishlistDiscount,
            Title = Title, Body = $"{w.ProductName} is {percent}% off, {period}.",
            DeduplicationKey = Key(discountId, w.ProductId, w.UserId), CreatedAt = DateTime.UtcNow
        }));
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException e)
        {
            // A parallel run already stored some of these keys; that run sends their emails.
            log?.LogWarning(e, "Wishlist notifications for discount {DiscountId} were already stored.", discountId);
            return 0;
        }

        var emails = fresh.GroupBy(w => w.UserId).ToList();
        var userEmails = await db.Users.AsNoTracking().Where(u => emails.Select(g => g.Key).Contains(u.Id))
            .Select(u => new { u.Id, u.Email }).ToDictionaryAsync(u => u.Id, u => u.Email, ct);
        var sent = 0;
        foreach (var group in emails)
        {
            if (!userEmails.TryGetValue(group.Key, out var recipient)) continue;
            var names = group.Select(w => w.ProductName).Distinct().ToList();
            var lines = names.Take(MaxListedProducts).Select(n => $"- {n}").ToList();
            if (names.Count > MaxListedProducts) lines.Add($"- and {names.Count - MaxListedProducts} more");
            var body = $"Products on your wishlist are {percent}% off, {period}:\n{string.Join('\n', lines)}";
            try
            {
                await sender.SendAsync(new DiscountEmailMessage(recipient, Title, body,
                    $"<wishlist-discount-{discountId}-{group.Key}@onlinestore.invalid>"), ct);
                sent++;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // The in-app notification is already stored; never log the address or the message.
                log?.LogWarning("Wishlist discount email for discount {DiscountId} could not be sent ({Error}).",
                    discountId, e.GetType().Name);
            }
        }
        return sent;
    }
}
