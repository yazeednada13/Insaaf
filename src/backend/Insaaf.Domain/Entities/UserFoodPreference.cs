namespace Insaaf.Domain.Entities;

/// <summary>A category a user chose in Taste Setup (Phase 11). Feeds «مقترح لك».</summary>
public sealed class UserFoodPreference
{
    /// <summary>The Identity user. The relationship is configured in Infrastructure.</summary>
    public Guid UserId { get; set; }

    public Guid CategoryId { get; set; }
}
