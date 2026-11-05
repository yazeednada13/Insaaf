namespace Insaaf.Domain.Entities;

/// <summary>
/// One user's like on one review. The composite key (ReviewId, UserId) makes a duplicate like
/// impossible. Social engagement never affects ratings.
/// </summary>
public sealed class ReviewLike
{
    public Guid ReviewId { get; set; }

    /// <summary>The Identity user. The relationship is configured in Infrastructure.</summary>
    public Guid UserId { get; set; }

    public DateTime CreatedAt { get; set; }
}
