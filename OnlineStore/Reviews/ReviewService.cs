using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using OnlineStore.Data;
using OnlineStore.Models;
using OnlineStore.Purchasing;

namespace OnlineStore.Reviews;

public sealed class ReviewService(StoreDbContext db, TimeProvider clock)
{
    public const int PageSize = 20;

    public async Task<ProductReviews> PublicAsync(long productId, long afterId, CancellationToken ct)
    {
        if (afterId < 0) throw new PurchaseException(400, "Use a non-negative comment cursor.");
        await RequireProductAsync(productId, ct);
        var ratings = db.Ratings.AsNoTracking().Where(r => r.ProductId == productId);
        var summary = await ratings.GroupBy(r => r.ProductId)
            .Select(group => new { Count = group.Count(), Average = group.Average(r => (double)r.Stars) })
            .SingleOrDefaultAsync(ct);
        // A public response must never include pending/rejected text or private account fields.
        var comments = await (from comment in db.Comments.AsNoTracking()
                              join author in db.Users.AsNoTracking() on comment.UserId equals author.Id
                              where comment.ProductId == productId && comment.ApprovalStatus == ApprovalStatus.Approved
                                  && comment.Id > afterId
                              orderby comment.Id
                              select new PublicComment(comment.Id, author.Name, comment.Body, comment.CreatedAt))
            .Take(PageSize + 1).ToListAsync(ct);
        return new(productId, summary?.Average, summary?.Count ?? 0, comments.Take(PageSize).ToList(),
            comments.Count > PageSize ? comments[PageSize - 1].Id : null);
    }

    public async Task<CustomerReview> OwnAsync(long userId, long productId, CancellationToken ct)
    {
        await RequireProductAsync(productId, ct);
        var lineId = await EligibleLines(userId, productId).OrderBy(line => line.Id)
            .Select(line => (long?)line.Id).FirstOrDefaultAsync(ct);
        var rating = await db.Ratings.AsNoTracking().Where(r => r.UserId == userId && r.ProductId == productId)
            .Select(r => (int?)r.Stars).SingleOrDefaultAsync(ct);
        var comment = await db.Comments.AsNoTracking().Where(c => c.UserId == userId && c.ProductId == productId)
            .Select(c => new OwnComment(c.Id, c.Body, c.ApprovalStatus.ToString(), c.CreatedAt)).SingleOrDefaultAsync(ct);
        return new(productId, lineId is not null, lineId, rating, comment,
            lineId is not null && rating is null, lineId is not null && comment is null);
    }

    public async Task<ReviewResult> SubmitAsync(long userId, long productId, ReviewSubmission input, CancellationToken ct)
    {
        if (input.Validate().Count != 0) throw new PurchaseException(400, "Enter a valid rating or comment.");
        await RequireProductAsync(productId, ct);
        // Serialize this customer's submissions, including retries before the original commit.
        // Existing unique (UserId, ProductId) keys remain the final database guard.
        await using IDbContextTransaction? transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct) : null;
        var user = db.Database.IsRelational()
            ? (await db.Users.FromSqlInterpolated($"SELECT * FROM Users WHERE Id = {userId} FOR UPDATE").ToListAsync(ct)).SingleOrDefault()
            : await db.Users.SingleOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null || !user.IsActive) throw new PurchaseException(401, "Please log in again.");
        if (user.Role != UserRole.Customer) throw new PurchaseException(403, "A customer account is required.");
        var lineId = await EligibleLines(userId, productId).OrderBy(line => line.Id)
            .Select(line => (long?)line.Id).FirstOrDefaultAsync(ct)
            ?? throw new PurchaseException(403, "Only products you purchased and received can be reviewed.");
        var rating = await db.Ratings.SingleOrDefaultAsync(r => r.UserId == userId && r.ProductId == productId, ct);
        var comment = await db.Comments.SingleOrDefaultAsync(c => c.UserId == userId && c.ProductId == productId, ct);
        var body = input.Comment?.Trim();
        // No partial write when either submitted component conflicts with an earlier review.
        if ((input.Rating is not null && rating is not null && rating.Stars != input.Rating)
            || (body is not null && comment is not null && comment.Body != body))
            throw new PurchaseException(409, "You already submitted this rating or comment. Refresh the product.");
        var now = clock.GetUtcNow().UtcDateTime;
        now = now.AddTicks(-(now.Ticks % 10));
        var created = false;
        if (input.Rating is not null && rating is null)
        {
            db.Ratings.Add(new Rating { UserId = userId, ProductId = productId, OrderItemId = lineId,
                Stars = (byte)input.Rating.Value, CreatedAt = now });
            created = true;
        }
        if (body is not null && comment is null)
        {
            db.Comments.Add(new Comment { UserId = userId, ProductId = productId, OrderItemId = lineId,
                Body = body, ApprovalStatus = ApprovalStatus.Pending, CreatedAt = now, UpdatedAt = now });
            created = true;
        }
        await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return new(await OwnAsync(userId, productId, ct), created);
    }

    private IQueryable<OrderItem> EligibleLines(long userId, long productId) => db.OrderItems.AsNoTracking()
        .Where(line => line.UserId == userId && line.Order.UserId == userId && line.ProductId == productId
            && line.DeliveredQuantity > 0 && line.Order.Status == OrderStatus.Delivered
            && line.Order.PurchasedAt != null && line.Order.DeliveredAt != null
            && (line.Order.PaymentStatus == PaymentStatus.Confirmed || line.Order.PaymentStatus == PaymentStatus.Refunded));

    private async Task RequireProductAsync(long productId, CancellationToken ct)
    {
        if (productId <= 0 || !await db.Products.AnyAsync(p => p.Id == productId && p.IsActive && p.Category.IsActive, ct))
            throw new PurchaseException(404, "Product not found.");
    }
}
