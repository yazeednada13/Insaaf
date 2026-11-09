namespace Insaaf.Domain.Entities;

/// <summary>
/// The restaurant's official reply to a review (Phase 11, ADR-021): one per review, written by an Admin
/// in the restaurant's name and shown as «رسمي». It never changes the review or its rating.
/// </summary>
public sealed class ReviewReply
{
    /// <summary>The review; also the key, so a review has at most one reply.</summary>
    public Guid ReviewId { get; set; }

    public string Text { get; set; } = string.Empty;

    /// <summary>The Admin who wrote or last replaced it. No foreign key (ADR-017).</summary>
    public Guid WrittenByUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
