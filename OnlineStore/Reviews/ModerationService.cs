using System.Data;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using OnlineStore.Data;
using OnlineStore.Models;
using OnlineStore.Purchasing;

namespace OnlineStore.Reviews;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CommentDecision(string? Decision, string? Note)
{
    public const int MaxNoteLength = 500;

    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        if (Decision is not ("Approve" or "Reject")) errors["decision"] = ["Use Approve or Reject."];
        if (Note is not null && Note.Trim().Length > MaxNoteLength)
            errors["note"] = [$"Use a note of at most {MaxNoteLength} characters."];
        return errors;
    }
}

// The moderation screen shows the comment, the product and the author's display name. No e-mail, address or tax data.
public sealed record ModerationComment(long Id, long ProductId, string ProductName, string AuthorName, int? Rating,
    string Body, string Status, DateTime CreatedAt, DateTime? ModeratedAt, string? ModerationNote);
public sealed record ModerationPage(string Status, IReadOnlyList<ModerationComment> Comments, long? NextAfterId);
public sealed record ModerationResult(ModerationComment Comment, bool Changed);

public sealed class ModerationService(StoreDbContext db, TimeProvider clock)
{
    public const int PageSize = 20;

    public async Task<ModerationPage> ListAsync(string? status, long afterId, CancellationToken ct)
    {
        // Only the three names count. Enum.TryParse would also accept a number such as "0".
        var wanted = ApprovalStatus.Pending;
        if (status is not null && (!Enum.GetNames<ApprovalStatus>().Contains(status)
                || !Enum.TryParse(status, ignoreCase: false, out wanted)))
            throw new PurchaseException(400, "Use Pending, Approved or Rejected.");
        if (afterId < 0) throw new PurchaseException(400, "Use a non-negative comment cursor.");
        var rows = await View(db.Comments.AsNoTracking().Where(c => c.ApprovalStatus == wanted && c.Id > afterId)
            .OrderBy(c => c.Id).Take(PageSize + 1)).ToListAsync(ct);
        return new(wanted.ToString(), rows.Take(PageSize).ToList(), rows.Count > PageSize ? rows[PageSize - 1].Id : null);
    }

    public async Task<ModerationResult> DecideAsync(long moderatorId, long commentId, CommentDecision input, CancellationToken ct)
    {
        if (input.Validate().Count != 0) throw new PurchaseException(400, "Use a valid decision and note.");
        if (commentId <= 0) throw new PurchaseException(404, "Comment not found.");
        var target = input.Decision == "Approve" ? ApprovalStatus.Approved : ApprovalStatus.Rejected;
        // The row lock makes two managers deciding the same comment at once take turns: the second one sees the result.
        await using IDbContextTransaction? transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct) : null;
        var moderator = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == moderatorId, ct);
        if (moderator is null || !moderator.IsActive) throw new PurchaseException(401, "Please log in again.");
        if (moderator.Role != UserRole.ProductManager) throw new PurchaseException(403, "A product manager account is required.");
        var comment = db.Database.IsRelational()
            ? (await db.Comments.FromSqlInterpolated($"SELECT * FROM Comments WHERE Id = {commentId} FOR UPDATE").ToListAsync(ct)).SingleOrDefault()
            : await db.Comments.SingleOrDefaultAsync(c => c.Id == commentId, ct);
        if (comment is null) throw new PurchaseException(404, "Comment not found.");
        var changed = false;
        if (comment.ApprovalStatus == ApprovalStatus.Pending)
        {
            var now = clock.GetUtcNow().UtcDateTime;
            now = now.AddTicks(-(now.Ticks % 10));
            comment.ApprovalStatus = target;
            comment.ModeratedByUserId = moderatorId;
            comment.ModeratedAt = now;
            comment.ModerationNote = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim();
            comment.UpdatedAt = now;
            await db.SaveChangesAsync(ct);
            changed = true;
        }
        // Repeating the same decision is a safe retry. A different decision on a decided comment is a stale screen.
        else if (comment.ApprovalStatus != target)
            throw new PurchaseException(409, "This comment was already decided. Refresh the list.");
        if (transaction is not null) await transaction.CommitAsync(ct);
        db.ChangeTracker.Clear();
        var view = await View(db.Comments.AsNoTracking().Where(c => c.Id == commentId)).SingleAsync(ct);
        return new(view, changed);
    }

    private IQueryable<ModerationComment> View(IQueryable<Comment> comments) =>
        from comment in comments
        join author in db.Users.AsNoTracking() on comment.UserId equals author.Id
        join product in db.Products.AsNoTracking() on comment.ProductId equals product.Id
        select new ModerationComment(comment.Id, comment.ProductId, product.Name, author.Name,
            db.Ratings.Where(r => r.UserId == comment.UserId && r.ProductId == comment.ProductId)
                .Select(r => (int?)r.Stars).FirstOrDefault(),
            comment.Body, comment.ApprovalStatus.ToString(), comment.CreatedAt, comment.ModeratedAt, comment.ModerationNote);
}
