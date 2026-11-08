using Insaaf.Domain.Entities;
using Insaaf.Domain.Enums;
using Insaaf.Domain.Repositories;
using Insaaf.Domain.Rules;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Insaaf.Infrastructure.Persistence.Repositories;

/// <summary>
/// Review photo persistence (Phase 9A plan §5–§8). On SQL Server every multi-statement change runs in a
/// transaction that locks the visit token row first, then photo rows, and is retried whole on deadlock.
/// The InMemory test provider has no transactions or raw SQL, so it uses tracked entities with the same
/// conditions; races are proven against SQL Server by probes.
/// </summary>
public sealed class ReviewPhotoRepository : IReviewPhotoRepository
{
    public const int MaxSlots = 5;

    private const int Pending = (int)ReviewPhotoStatus.Pending;
    private const int Ready = (int)ReviewPhotoStatus.Ready;
    private const int Deleting = (int)ReviewPhotoStatus.Deleting;
    private const int Purged = (int)ReviewPhotoStatus.Purged;

    private readonly AppDbContext _dbContext;

    public ReviewPhotoRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    private bool IsRelational => _dbContext.Database.IsRelational();

    public Task<ReviewPhoto?> GetAsync(Guid photoId, CancellationToken cancellationToken = default) =>
        _dbContext.ReviewPhotos.AsNoTracking().SingleOrDefaultAsync(photo => photo.Id == photoId, cancellationToken);

    public Task<ReviewPhoto?> FindByClientUploadIdAsync(
        Guid visitTokenId,
        Guid userId,
        Guid clientUploadId,
        CancellationToken cancellationToken = default) =>
        _dbContext.ReviewPhotos.AsNoTracking().SingleOrDefaultAsync(
            photo => photo.VisitTokenId == visitTokenId
                && photo.UploadedByUserId == userId
                && photo.ClientUploadId == clientUploadId,
            cancellationToken);

    public async Task<IReadOnlyList<ReviewPhoto>> ListByIdsAsync(
        IReadOnlyCollection<Guid> photoIds,
        CancellationToken cancellationToken = default) =>
        photoIds.Count == 0
            ? []
            : await _dbContext.ReviewPhotos.AsNoTracking().Where(photo => photoIds.Contains(photo.Id)).ToListAsync(cancellationToken);

    public Task<PhotoReservationResult> ReserveAsync(
        ReviewPhoto photo,
        DateTime utcNow,
        CancellationToken cancellationToken = default) =>
        IsRelational
            ? SqlDeadlockRetry.RunAsync(_dbContext, () => ReserveRelationalAsync(photo, utcNow, cancellationToken), cancellationToken)
            : ReserveInMemoryAsync(photo, utcNow, cancellationToken);

    private async Task<PhotoReservationResult> ReserveRelationalAsync(ReviewPhoto photo, DateTime utcNow, CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Token first: reserve a slot and take (or confirm) the claim in one conditional statement.
        var reserved = await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE VisitTokens
            SET PhotoSlotsUsed = PhotoSlotsUsed + 1,
                PhotoClaimedByUserId = COALESCE(PhotoClaimedByUserId, {photo.UploadedByUserId})
            WHERE Id = {photo.VisitTokenId}
              AND RedeemedAt IS NULL AND RevokedAt IS NULL AND ExpiresAt > {utcNow}
              AND PhotoSlotsUsed < {MaxSlots}
              AND (PhotoClaimedByUserId IS NULL OR PhotoClaimedByUserId = {photo.UploadedByUserId})
            """,
            cancellationToken);

        if (reserved == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return await ExplainRefusalAsync(photo.VisitTokenId, photo.UploadedByUserId, utcNow, cancellationToken);
        }

        _dbContext.ReviewPhotos.Add(photo);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // A concurrent request inserted the same logical photo; the rollback returns the slot.
            await transaction.RollbackAsync(cancellationToken);
            _dbContext.ChangeTracker.Clear();
            return PhotoReservationResult.Duplicate;
        }

        await transaction.CommitAsync(cancellationToken);
        _dbContext.Entry(photo).State = EntityState.Detached;
        return PhotoReservationResult.Reserved;
    }

    private async Task<PhotoReservationResult> ReserveInMemoryAsync(ReviewPhoto photo, DateTime utcNow, CancellationToken cancellationToken)
    {
        var token = await _dbContext.VisitTokens.SingleAsync(item => item.Id == photo.VisitTokenId, cancellationToken);
        if (token.RedeemedAt is not null || token.RevokedAt is not null || token.ExpiresAt <= utcNow)
        {
            return PhotoReservationResult.TokenUnavailable;
        }

        if (token.PhotoClaimedByUserId is { } claimant && claimant != photo.UploadedByUserId)
        {
            return PhotoReservationResult.Claimed;
        }

        if (token.PhotoSlotsUsed >= MaxSlots)
        {
            return PhotoReservationResult.LimitReached;
        }

        if (await _dbContext.ReviewPhotos.AnyAsync(
                item => item.VisitTokenId == photo.VisitTokenId
                    && item.UploadedByUserId == photo.UploadedByUserId
                    && item.ClientUploadId == photo.ClientUploadId,
                cancellationToken))
        {
            return PhotoReservationResult.Duplicate;
        }

        token.PhotoSlotsUsed++;
        token.PhotoClaimedByUserId ??= photo.UploadedByUserId;
        _dbContext.ReviewPhotos.Add(photo);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _dbContext.ChangeTracker.Clear();
        return PhotoReservationResult.Reserved;
    }

    private async Task<PhotoReservationResult> ExplainRefusalAsync(
        Guid visitTokenId,
        Guid userId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var token = await _dbContext.VisitTokens.AsNoTracking().SingleAsync(item => item.Id == visitTokenId, cancellationToken);
        if (token.RedeemedAt is not null || token.RevokedAt is not null || token.ExpiresAt <= utcNow)
        {
            return PhotoReservationResult.TokenUnavailable;
        }

        return token.PhotoClaimedByUserId is { } claimant && claimant != userId
            ? PhotoReservationResult.Claimed
            : PhotoReservationResult.LimitReached;
    }

    public async Task<bool> TryTakeLeaseAsync(
        Guid photoId,
        Guid leaseId,
        DateTime utcNow,
        DateTime leaseExpiresAt,
        CancellationToken cancellationToken = default)
    {
        if (IsRelational)
        {
            return await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE ReviewPhotos
                SET LeaseId = {leaseId}, LeaseExpiresAt = {leaseExpiresAt}, UpdatedAt = {utcNow}
                WHERE Id = {photoId} AND Status = {Pending} AND (LeaseExpiresAt IS NULL OR LeaseExpiresAt < {utcNow})
                """,
                cancellationToken) == 1;
        }

        return await UpdateTrackedAsync(
            photoId,
            photo => photo.Status == ReviewPhotoStatus.Pending && (photo.LeaseExpiresAt is null || photo.LeaseExpiresAt < utcNow),
            photo =>
            {
                photo.LeaseId = leaseId;
                photo.LeaseExpiresAt = leaseExpiresAt;
                photo.UpdatedAt = utcNow;
            },
            cancellationToken);
    }

    public async Task<bool> CompleteAsync(
        Guid photoId,
        Guid leaseId,
        string storageKey,
        int width,
        int height,
        int sizeBytes,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        if (IsRelational)
        {
            return await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE ReviewPhotos
                SET Status = {Ready}, StorageKey = {storageKey}, Width = {width}, Height = {height}, SizeBytes = {sizeBytes},
                    LeaseId = NULL, LeaseExpiresAt = NULL, UpdatedAt = {utcNow}
                WHERE Id = {photoId} AND Status = {Pending} AND LeaseId = {leaseId}
                """,
                cancellationToken) == 1;
        }

        return await UpdateTrackedAsync(
            photoId,
            photo => photo.Status == ReviewPhotoStatus.Pending && photo.LeaseId == leaseId,
            photo =>
            {
                photo.Status = ReviewPhotoStatus.Ready;
                photo.StorageKey = storageKey;
                photo.Width = width;
                photo.Height = height;
                photo.SizeBytes = sizeBytes;
                photo.LeaseId = null;
                photo.LeaseExpiresAt = null;
                photo.UpdatedAt = utcNow;
            },
            cancellationToken);
    }

    public async Task<bool> AbandonAttemptAsync(
        Guid photoId,
        Guid leaseId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        if (IsRelational)
        {
            return await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE ReviewPhotos
                SET Status = {Deleting}, DeletedAt = {utcNow}, LeaseId = NULL, LeaseExpiresAt = NULL, UpdatedAt = {utcNow}
                WHERE Id = {photoId} AND Status = {Pending} AND LeaseId = {leaseId}
                """,
                cancellationToken) == 1;
        }

        return await UpdateTrackedAsync(
            photoId,
            photo => photo.Status == ReviewPhotoStatus.Pending && photo.LeaseId == leaseId,
            photo =>
            {
                photo.Status = ReviewPhotoStatus.Deleting;
                photo.DeletedAt = utcNow;
                photo.LeaseId = null;
                photo.LeaseExpiresAt = null;
                photo.UpdatedAt = utcNow;
            },
            cancellationToken);
    }

    public Task<bool> TombstoneAsync(
        Guid photoId,
        Guid visitTokenId,
        Guid? deletedByUserId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        if (!IsRelational)
        {
            return UpdateTrackedAsync(
                photoId,
                photo => photo.Status is ReviewPhotoStatus.Pending or ReviewPhotoStatus.Ready,
                photo =>
                {
                    photo.Status = ReviewPhotoStatus.Deleting;
                    photo.DeletedAt = utcNow;
                    photo.DeletedByUserId = deletedByUserId;
                    photo.LeaseId = null;
                    photo.LeaseExpiresAt = null;
                    photo.UpdatedAt = utcNow;
                },
                cancellationToken);
        }

        return SqlDeadlockRetry.RunAsync(_dbContext, async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            await LockTokenAsync(visitTokenId, cancellationToken);
            var rows = await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE ReviewPhotos
                SET Status = {Deleting}, DeletedAt = {utcNow}, DeletedByUserId = {deletedByUserId},
                    LeaseId = NULL, LeaseExpiresAt = NULL, UpdatedAt = {utcNow}
                WHERE Id = {photoId} AND VisitTokenId = {visitTokenId} AND Status IN ({Pending}, {Ready})
                """,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return rows == 1;
        }, cancellationToken);
    }

    public Task<bool> PurgeAsync(Guid photoId, Guid visitTokenId, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        if (!IsRelational)
        {
            return PurgeInMemoryAsync(photoId, visitTokenId, utcNow, cancellationToken);
        }

        return SqlDeadlockRetry.RunAsync(_dbContext, async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            await LockTokenAsync(visitTokenId, cancellationToken);

            var wasUnattached = await _dbContext.ReviewPhotos.AsNoTracking()
                .Where(photo => photo.Id == photoId && photo.VisitTokenId == visitTokenId && photo.Status == ReviewPhotoStatus.Deleting)
                .Select(photo => (bool?)(photo.ReviewId == null))
                .SingleOrDefaultAsync(cancellationToken);
            if (wasUnattached is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE ReviewPhotos
                SET Status = {Purged}, PurgedAt = {utcNow}, LeaseId = NULL, LeaseExpiresAt = NULL, UpdatedAt = {utcNow}
                WHERE Id = {photoId} AND Status = {Deleting}
                """,
                cancellationToken);

            if (wasUnattached.Value)
            {
                await ReleaseSlotAsync(visitTokenId, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return true;
        }, cancellationToken);
    }

    private async Task<bool> PurgeInMemoryAsync(Guid photoId, Guid visitTokenId, DateTime utcNow, CancellationToken cancellationToken)
    {
        var token = await _dbContext.VisitTokens.SingleAsync(item => item.Id == visitTokenId, cancellationToken);
        var photo = await _dbContext.ReviewPhotos.SingleOrDefaultAsync(
            item => item.Id == photoId && item.VisitTokenId == visitTokenId && item.Status == ReviewPhotoStatus.Deleting,
            cancellationToken);
        if (photo is null)
        {
            _dbContext.ChangeTracker.Clear();
            return false;
        }

        photo.Status = ReviewPhotoStatus.Purged;
        photo.PurgedAt = utcNow;
        photo.LeaseId = null;
        photo.LeaseExpiresAt = null;
        photo.UpdatedAt = utcNow;

        if (photo.ReviewId is null && token.PhotoSlotsUsed > 0)
        {
            token.PhotoSlotsUsed--;
            await _dbContext.SaveChangesAsync(cancellationToken);
            var claimStillLive = token.PhotoClaimedByUserId is { } claimant && await _dbContext.ReviewPhotos.AnyAsync(
                item => item.VisitTokenId == visitTokenId
                    && item.UploadedByUserId == claimant
                    && item.Status != ReviewPhotoStatus.Purged,
                cancellationToken);
            if (!claimStillLive)
            {
                token.PhotoClaimedByUserId = null;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _dbContext.ChangeTracker.Clear();
        return true;
    }

    public Task<string?> GetServableKeyAsync(Guid photoId, CancellationToken cancellationToken = default) =>
        _dbContext.ReviewPhotos.AsNoTracking()
            .Where(photo => photo.Id == photoId && photo.Status == ReviewPhotoStatus.Ready && photo.ReviewId != null)
            .Where(photo => _dbContext.PublicReviewsOfActiveRestaurants().Any(review => review.Id == photo.ReviewId))
            .Select(photo => photo.StorageKey)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ReviewPhoto>> ListStalePendingAsync(
        DateTime updatedBefore,
        DateTime utcNow,
        int take,
        CancellationToken cancellationToken = default) =>
        await _dbContext.ReviewPhotos.AsNoTracking()
            .Where(photo => photo.Status == ReviewPhotoStatus.Pending
                && photo.UpdatedAt < updatedBefore
                && (photo.LeaseExpiresAt == null || photo.LeaseExpiresAt < utcNow))
            .OrderBy(photo => photo.UpdatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ReviewPhoto>> ListExpiredUnattachedAsync(
        DateTime createdBefore,
        DateTime utcNow,
        int take,
        CancellationToken cancellationToken = default) =>
        await _dbContext.ReviewPhotos.AsNoTracking()
            .Where(photo => photo.Status == ReviewPhotoStatus.Ready && photo.ReviewId == null)
            .Where(photo => photo.CreatedAt < createdBefore
                || _dbContext.VisitTokens.Any(token => token.Id == photo.VisitTokenId
                    && (token.RedeemedAt != null || token.RevokedAt != null || token.ExpiresAt <= utcNow)))
            .OrderBy(photo => photo.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ReviewPhoto>> ListDeletingAsync(int take, CancellationToken cancellationToken = default) =>
        await _dbContext.ReviewPhotos.AsNoTracking()
            .Where(photo => photo.Status == ReviewPhotoStatus.Deleting)
            .OrderBy(photo => photo.UpdatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ReviewPhoto>> ListPrunableAsync(
        DateTime purgedBefore,
        int take,
        CancellationToken cancellationToken = default) =>
        await _dbContext.ReviewPhotos.AsNoTracking()
            .Where(photo => photo.Status == ReviewPhotoStatus.Purged && photo.PurgedAt < purgedBefore)
            .OrderBy(photo => photo.PurgedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

    public async Task<bool> PruneAsync(Guid photoId, CancellationToken cancellationToken = default)
    {
        if (IsRelational)
        {
            return await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM ReviewPhotos WHERE Id = {photoId} AND Status = {Purged}",
                cancellationToken) == 1;
        }

        var photo = await _dbContext.ReviewPhotos.SingleOrDefaultAsync(
            item => item.Id == photoId && item.Status == ReviewPhotoStatus.Purged,
            cancellationToken);
        if (photo is null)
        {
            return false;
        }

        _dbContext.ReviewPhotos.Remove(photo);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _dbContext.ChangeTracker.Clear();
        return true;
    }

    public async Task<int> RepairTokenCountersAsync(CancellationToken cancellationToken = default)
    {
        if (!IsRelational)
        {
            return 0;
        }

        // Candidates only; each is re-checked under its own token lock below.
        var candidates = await _dbContext.VisitTokens.AsNoTracking()
            .Where(token => token.PhotoSlotsUsed > 0 || token.PhotoClaimedByUserId != null)
            .Select(token => token.Id)
            .ToListAsync(cancellationToken);

        var repaired = 0;
        foreach (var tokenId in candidates)
        {
            try
            {
                repaired += await SqlDeadlockRetry.RunAsync(_dbContext, () => RepairTokenAsync(tokenId, cancellationToken), cancellationToken);
            }
            catch (DbException)
            {
                // One token that can't be repaired (e.g. more live rows than the CHECK allows) never blocks
                // the others; its extra rows are removed by cleanup and the next run repairs it.
                _dbContext.ChangeTracker.Clear();
            }
        }

        return repaired;
    }

    // The database reads with READ_COMMITTED_SNAPSHOT, so a statement sees data as of its own start. The
    // token is locked by one statement and counted by the next: every writer for this token holds the token
    // lock until it commits, so the count can't miss an in-flight upload or purge.
    private async Task<int> RepairTokenAsync(Guid tokenId, CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await LockTokenAsync(tokenId, cancellationToken);
        var rows = await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE VisitTokens
            SET PhotoSlotsUsed = (
                    SELECT COUNT(*) FROM ReviewPhotos p
                    WHERE p.VisitTokenId = {tokenId} AND p.ReviewId IS NULL AND p.Status IN ({Pending}, {Ready}, {Deleting})),
                PhotoClaimedByUserId = CASE WHEN EXISTS (
                        SELECT 1 FROM ReviewPhotos p
                        WHERE p.VisitTokenId = {tokenId} AND p.UploadedByUserId = VisitTokens.PhotoClaimedByUserId
                          AND p.Status IN ({Pending}, {Ready}, {Deleting}))
                    THEN PhotoClaimedByUserId ELSE NULL END
            WHERE Id = {tokenId}
              AND (PhotoSlotsUsed <> (
                        SELECT COUNT(*) FROM ReviewPhotos p
                        WHERE p.VisitTokenId = {tokenId} AND p.ReviewId IS NULL AND p.Status IN ({Pending}, {Ready}, {Deleting}))
                   OR (PhotoClaimedByUserId IS NOT NULL AND NOT EXISTS (
                        SELECT 1 FROM ReviewPhotos p
                        WHERE p.VisitTokenId = {tokenId} AND p.UploadedByUserId = VisitTokens.PhotoClaimedByUserId
                          AND p.Status IN ({Pending}, {Ready}, {Deleting}))))
            """,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return rows;
    }

    private Task LockTokenAsync(Guid visitTokenId, CancellationToken cancellationToken) =>
        _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM VisitTokens WITH (UPDLOCK, ROWLOCK) WHERE Id = {visitTokenId}",
            cancellationToken);

    // Called inside a transaction that already holds the token lock.
    private Task ReleaseSlotAsync(Guid visitTokenId, CancellationToken cancellationToken) =>
        _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE VisitTokens
            SET PhotoSlotsUsed = PhotoSlotsUsed - 1,
                PhotoClaimedByUserId = CASE WHEN EXISTS (
                        SELECT 1 FROM ReviewPhotos p
                        WHERE p.VisitTokenId = {visitTokenId} AND p.UploadedByUserId = VisitTokens.PhotoClaimedByUserId
                          AND p.Status IN ({Pending}, {Ready}, {Deleting}))
                    THEN PhotoClaimedByUserId ELSE NULL END
            WHERE Id = {visitTokenId} AND PhotoSlotsUsed > 0
            """,
            cancellationToken);

    private async Task<bool> UpdateTrackedAsync(
        Guid photoId,
        Func<ReviewPhoto, bool> condition,
        Action<ReviewPhoto> apply,
        CancellationToken cancellationToken)
    {
        var photo = await _dbContext.ReviewPhotos.SingleOrDefaultAsync(item => item.Id == photoId, cancellationToken);
        if (photo is null || !condition(photo))
        {
            _dbContext.ChangeTracker.Clear();
            return false;
        }

        apply(photo);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _dbContext.ChangeTracker.Clear();
        return true;
    }
}
