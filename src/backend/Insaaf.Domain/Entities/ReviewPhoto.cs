using Insaaf.Domain.Enums;

namespace Insaaf.Domain.Entities;

/// <summary>
/// A photo uploaded with a visit token and attached to the verified review created from that token
/// (Phase 9A, ADR-018). The row tracks the photo's whole storage folder, and it is never removed
/// before that folder is confirmed empty. Photos never affect ratings.
/// </summary>
public sealed class ReviewPhoto
{
    public Guid Id { get; set; }

    /// <summary>The app's id for one logical photo; makes retried uploads idempotent.</summary>
    public Guid ClientUploadId { get; set; }

    /// <summary>Null until the photo is attached in the review's redeem transaction.</summary>
    public Guid? ReviewId { get; set; }

    public Guid VisitTokenId { get; set; }

    public Guid UploadedByUserId { get; set; }

    public ReviewPhotoStatus Status { get; set; }

    /// <summary>The served file. Set only by the fenced completion of a processing attempt.</summary>
    public string? StorageKey { get; set; }

    public string ContentType { get; set; } = "image/jpeg";

    public int? Width { get; set; }

    public int? Height { get; set; }

    public int? SizeBytes { get; set; }

    /// <summary>Position in the review, 0–4; set when attached.</summary>
    public int? SortOrder { get; set; }

    /// <summary>The processing attempt currently allowed to complete this photo.</summary>
    public Guid? LeaseId { get; set; }

    public DateTime? LeaseExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime? AttachedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public DateTime? PurgedAt { get; set; }

    /// <summary>The Admin who removed it, when removed by an Admin.</summary>
    public Guid? DeletedByUserId { get; set; }
}
