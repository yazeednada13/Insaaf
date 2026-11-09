using Insaaf.Domain.Entities;

namespace Insaaf.Domain.Repositories;

public enum PhotoReservationResult
{
    Reserved,

    /// <summary>A row with the same token, user and client upload id already exists.</summary>
    Duplicate,

    /// <summary>The token already has 5 unattached live photos.</summary>
    LimitReached,

    /// <summary>Another user holds the token's photo claim.</summary>
    Claimed,

    /// <summary>The token is redeemed, revoked or expired.</summary>
    TokenUnavailable,
}

/// <summary>Thrown when a database operation still deadlocks after its retries; nothing was applied.</summary>
public sealed class RepositoryBusyException : Exception
{
    public RepositoryBusyException(Exception inner)
        : base("The operation could not complete because of concurrent activity.", inner)
    {
    }
}

/// <summary>
/// Review photo persistence (Phase 9A). Every multi-statement change locks the visit token row first, then
/// photo rows, and keeps <c>PhotoSlotsUsed</c> and the claim consistent in the same transaction.
/// Single-row statements (lease, completion, abandoning an attempt) are atomic on their own.
/// </summary>
public interface IReviewPhotoRepository
{
    Task<ReviewPhoto?> GetAsync(Guid photoId, CancellationToken cancellationToken = default);

    Task<ReviewPhoto?> FindByClientUploadIdAsync(
        Guid visitTokenId,
        Guid userId,
        Guid clientUploadId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReviewPhoto>> ListByIdsAsync(IReadOnlyCollection<Guid> photoIds, CancellationToken cancellationToken = default);

    /// <summary>Reserves a slot, takes the claim and inserts the Pending row (with its lease) atomically.</summary>
    Task<PhotoReservationResult> ReserveAsync(ReviewPhoto photo, DateTime utcNow, CancellationToken cancellationToken = default);

    /// <summary>Takes the processing lease of a Pending row whose lease is free or expired.</summary>
    Task<bool> TryTakeLeaseAsync(
        Guid photoId,
        Guid leaseId,
        DateTime utcNow,
        DateTime leaseExpiresAt,
        CancellationToken cancellationToken = default);

    /// <summary>The fence: marks the row Ready with this attempt's file, only while the attempt holds the lease.</summary>
    Task<bool> CompleteAsync(
        Guid photoId,
        Guid leaseId,
        string storageKey,
        int width,
        int height,
        int sizeBytes,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    /// <summary>A failed attempt tombstones its own row and clears its lease in one statement.</summary>
    Task<bool> AbandonAttemptAsync(Guid photoId, Guid leaseId, DateTime utcNow, CancellationToken cancellationToken = default);

    /// <summary>Pending or Ready → Deleting, clearing any lease. Invisible from then on.</summary>
    Task<bool> TombstoneAsync(
        Guid photoId,
        Guid visitTokenId,
        Guid? deletedByUserId,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    /// <summary>Deleting → Purged after its folder was confirmed empty; releases the slot and claim when unattached.</summary>
    Task<bool> PurgeAsync(Guid photoId, Guid visitTokenId, DateTime utcNow, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ready photos of the restaurant's public reviews, most recently attached first (Phase 11). The same
    /// visibility as <see cref="GetServableKeyAsync"/>, so every listed photo can be served.
    /// </summary>
    Task<IReadOnlyList<Insaaf.Domain.Repositories.Projections.RestaurantPhotoProjection>> ListForRestaurantAsync(
        Guid restaurantId,
        FeedCursor? after,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>The served file of a Ready, attached photo of a public review in an active restaurant.</summary>
    Task<string?> GetServableKeyAsync(Guid photoId, CancellationToken cancellationToken = default);

    // Cleanup (Phase 9A plan §8).

    Task<IReadOnlyList<ReviewPhoto>> ListStalePendingAsync(
        DateTime updatedBefore,
        DateTime utcNow,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>Ready unattached photos older than the cutoff, or of a redeemed, revoked or expired token.</summary>
    Task<IReadOnlyList<ReviewPhoto>> ListExpiredUnattachedAsync(
        DateTime createdBefore,
        DateTime utcNow,
        int take,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReviewPhoto>> ListDeletingAsync(int take, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReviewPhoto>> ListPrunableAsync(DateTime purgedBefore, int take, CancellationToken cancellationToken = default);

    /// <summary>Deletes a Purged tombstone row.</summary>
    Task<bool> PruneAsync(Guid photoId, CancellationToken cancellationToken = default);

    /// <summary>Recomputes slot counts and claims for unredeemed tokens; returns how many tokens were fixed.</summary>
    Task<int> RepairTokenCountersAsync(CancellationToken cancellationToken = default);
}
