using System.Text.Json.Serialization;

namespace OnlineStore.Reviews;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReviewSubmission(int? Rating, string? Comment)
{
    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        if (Rating is < 1 or > 5) errors["rating"] = ["Use a rating between 1 and 5."];
        if (Comment is not null && (string.IsNullOrWhiteSpace(Comment) || Comment.Trim().Length > 2000))
            errors["comment"] = ["Use a non-blank comment of at most 2000 characters."];
        if (Rating is null && Comment is null) errors["review"] = ["Submit a rating, a comment, or both."];
        return errors;
    }
}

public sealed record PublicComment(long Id, string AuthorName, string Body, DateTime CreatedAt);
public sealed record ProductReviews(long ProductId, double? AverageRating, int RatingCount,
    IReadOnlyList<PublicComment> Comments, long? NextAfterId);
public sealed record OwnComment(long Id, string Body, string Status, DateTime CreatedAt);
public sealed record CustomerReview(long ProductId, bool Eligible, long? OrderItemId, int? Rating,
    OwnComment? Comment, bool CanRate, bool CanComment);
public sealed record ReviewResult(CustomerReview Review, bool Created);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ModerationRequest(string? Status, string? Version);
public sealed record ModerationComment(long Id, long ProductId, string ProductName, string CustomerName,
    int? Rating, string Body, DateTime CreatedAt, string Version);
public sealed record ModerationPage(IReadOnlyList<ModerationComment> Items, long? NextAfterId);
public sealed record ModerationResult(long Id, string Status, string Version);
