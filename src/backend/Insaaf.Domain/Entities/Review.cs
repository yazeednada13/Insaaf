namespace Insaaf.Domain.Entities;

/// <summary>
/// A restaurant review. In the MVP every review is created through the verified QR flow
/// (Phase 5): <see cref="IsVerified"/> is true and <see cref="VisitTokenId"/> identifies the redeemed
/// visit. Only verified reviews are ever returned by public read queries.
/// </summary>
public sealed class Review
{
    public Guid Id { get; set; }

    public Guid RestaurantId { get; set; }

    public Restaurant Restaurant { get; set; } = null!;

    /// <summary>The reviewer (Identity user). The relationship is configured in Infrastructure.</summary>
    public Guid UserId { get; set; }

    /// <summary>Overall rating, 1–5.</summary>
    public int Rating { get; set; }

    public int FoodRating { get; set; }

    public int ServiceRating { get; set; }

    public int ValueRating { get; set; }

    public int AtmosphereRating { get; set; }

    public string Text { get; set; } = string.Empty;

    public bool IsVerified { get; set; }

    /// <summary>The redeemed visit token; required whenever <see cref="IsVerified"/> is true.</summary>
    public Guid? VisitTokenId { get; set; }

    public DateTime CreatedAt { get; set; }
}
