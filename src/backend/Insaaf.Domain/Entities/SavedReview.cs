namespace Insaaf.Domain.Entities;

/// <summary>A review a user saved (Phase 11). Private to that user; one row per pair.</summary>
public sealed class SavedReview
{
    /// <summary>The Identity user. The relationship is configured in Infrastructure.</summary>
    public Guid UserId { get; set; }

    public Guid ReviewId { get; set; }

    public DateTime CreatedAt { get; set; }
}
