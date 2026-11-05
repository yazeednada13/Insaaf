namespace Insaaf.Domain.Entities;

/// <summary>A flat comment on a review. Only its author can delete it. Never affects ratings.</summary>
public sealed class ReviewComment
{
    public Guid Id { get; set; }

    public Guid ReviewId { get; set; }

    /// <summary>The author (Identity user). The relationship is configured in Infrastructure.</summary>
    public Guid UserId { get; set; }

    public string Text { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
