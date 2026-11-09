namespace Insaaf.Domain.Entities;

/// <summary>One user's like on one comment (Phase 11). The composite key prevents duplicates.</summary>
public sealed class ReviewCommentLike
{
    public Guid CommentId { get; set; }

    /// <summary>The Identity user. The relationship is configured in Infrastructure.</summary>
    public Guid UserId { get; set; }

    public DateTime CreatedAt { get; set; }
}
