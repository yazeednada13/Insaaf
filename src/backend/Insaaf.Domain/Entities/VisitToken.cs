using Insaaf.Domain.Enums;

namespace Insaaf.Domain.Entities;

/// <summary>
/// A single-use proof of a visit, printed as a QR on one invoice. Its restaurant is fixed at issuance
/// and is the only source of the restaurant for the verified review it creates. Only the SHA-256 hash
/// of the secret token is stored.
/// </summary>
public sealed class VisitToken
{
    public Guid Id { get; set; }

    public Guid RestaurantId { get; set; }

    public Restaurant Restaurant { get; set; } = null!;

    /// <summary>Groups the tokens created by one issuance request (used for revocation).</summary>
    public Guid IssuanceId { get; set; }

    public VisitTokenIssuanceChannel IssuanceChannel { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public DateTime IssuedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    /// <summary>The issuing Admin for manual tokens; null for channels without a user.</summary>
    public Guid? IssuedByUserId { get; set; }

    public DateTime? RedeemedAt { get; set; }

    public Guid? RedeemedByUserId { get; set; }

    public DateTime? RevokedAt { get; set; }

    /// <summary>Optimistic concurrency token: changes on redemption so two redemptions cannot both succeed.</summary>
    public Guid ConcurrencyStamp { get; set; }

    /// <summary>
    /// The user who uploaded photos with this token (Phase 9A). While set, only that user can upload or
    /// redeem the token. Released when that user's last live photo row for the token is purged.
    /// </summary>
    public Guid? PhotoClaimedByUserId { get; set; }

    /// <summary>Unattached live photo rows (Pending, Ready, Deleting) for this token; at most 5.</summary>
    public int PhotoSlotsUsed { get; set; }
}
