using Insaaf.Domain.Entities;
using Insaaf.Domain.Enums;
using Insaaf.Domain.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Insaaf.Infrastructure.Persistence.Repositories;

public sealed class VisitTokenRepository : IVisitTokenRepository
{
    private const int MaxRevokeAttempts = 3;

    private readonly AppDbContext _dbContext;

    public VisitTokenRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<VisitToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        _dbContext.VisitTokens
            .Include(token => token.Restaurant)
            .SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

    public async Task AddRangeAsync(IReadOnlyCollection<VisitToken> tokens, CancellationToken cancellationToken = default)
    {
        _dbContext.VisitTokens.AddRange(tokens);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> RevokeUnusedAsync(
        Guid restaurantId,
        Guid? issuanceId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        // Revocation also changes the concurrency stamp, so it can never overwrite a redemption that
        // happened in between; on conflict the unused set is reloaded and revoked again.
        for (var attempt = 1; ; attempt++)
        {
            var unused = await _dbContext.VisitTokens
                .Where(token => token.RestaurantId == restaurantId
                    && token.RedeemedAt == null
                    && token.RevokedAt == null
                    && (issuanceId == null || token.IssuanceId == issuanceId))
                .ToListAsync(cancellationToken);

            foreach (var token in unused)
            {
                token.RevokedAt = utcNow;
                token.ConcurrencyStamp = Guid.NewGuid();
            }

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                return unused.Count;
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxRevokeAttempts)
            {
                _dbContext.ChangeTracker.Clear();
            }
        }
    }

    public async Task<VisitTokenSummary> GetSummaryAsync(
        Guid restaurantId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var tokens = _dbContext.VisitTokens.AsNoTracking().Where(token => token.RestaurantId == restaurantId);

        var redeemed = await tokens.CountAsync(token => token.RedeemedAt != null, cancellationToken);
        var revoked = await tokens.CountAsync(
            token => token.RedeemedAt == null && token.RevokedAt != null,
            cancellationToken);
        var expired = await tokens.CountAsync(
            token => token.RedeemedAt == null && token.RevokedAt == null && token.ExpiresAt <= utcNow,
            cancellationToken);
        var active = await tokens.CountAsync(
            token => token.RedeemedAt == null && token.RevokedAt == null && token.ExpiresAt > utcNow,
            cancellationToken);

        return new VisitTokenSummary(active, redeemed, expired, revoked);
    }

    public Task<RedemptionResult> RedeemWithReviewAsync(
        VisitToken token,
        Review review,
        IReadOnlyList<Guid> photoIds,
        CancellationToken cancellationToken = default) =>
        _dbContext.Database.IsRelational()
            ? SqlDeadlockRetry.RunAsync(
                _dbContext,
                () => RedeemRelationalAsync(token.Id, token.ConcurrencyStamp, review, photoIds, cancellationToken),
                cancellationToken)
            : RedeemInMemoryAsync(token, review, photoIds, cancellationToken);

    // One transaction, token row first (Phase 9A plan section 5): redeem, enforce the photo claim and
    // release the attached photos' slots in one conditional UPDATE, insert the review, then attach every
    // photo. Any mismatch rolls everything back, so the token stays unredeemed.
    private async Task<RedemptionResult> RedeemRelationalAsync(
        Guid tokenId,
        Guid readStamp,
        Review review,
        IReadOnlyList<Guid> photoIds,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var photoCount = photoIds.Count;
        var userId = review.UserId;
        var now = review.CreatedAt;

        var redeemed = await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE VisitTokens
            SET RedeemedAt = {now}, RedeemedByUserId = {userId}, ConcurrencyStamp = {Guid.NewGuid()},
                PhotoSlotsUsed = PhotoSlotsUsed - {photoCount}
            WHERE Id = {tokenId} AND ConcurrencyStamp = {readStamp}
              AND RedeemedAt IS NULL AND RevokedAt IS NULL
              AND (PhotoClaimedByUserId IS NULL OR PhotoClaimedByUserId = {userId})
              AND PhotoSlotsUsed >= {photoCount}
            """,
            cancellationToken);

        if (redeemed == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            var current = await _dbContext.VisitTokens.AsNoTracking().SingleAsync(item => item.Id == tokenId, cancellationToken);
            if (current.RedeemedAt is not null || current.RevokedAt is not null || current.ConcurrencyStamp != readStamp)
            {
                return RedemptionResult.AlreadyRedeemed;
            }

            return current.PhotoClaimedByUserId is { } claimant && claimant != userId
                ? RedemptionResult.TokenClaimed
                : RedemptionResult.PhotosInvalid;
        }

        // The tracked token was not modified, so SaveChanges inserts only the review.
        _dbContext.Reviews.Add(review);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            // Last guard: the filtered unique index on Reviews.VisitTokenId.
            await transaction.RollbackAsync(cancellationToken);
            _dbContext.ChangeTracker.Clear();
            return RedemptionResult.AlreadyRedeemed;
        }

        var attached = 0;
        for (var position = 0; position < photoCount; position++)
        {
            attached += await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE ReviewPhotos
                SET ReviewId = {review.Id}, SortOrder = {position}, AttachedAt = {now}, UpdatedAt = {now}
                WHERE Id = {photoIds[position]} AND VisitTokenId = {tokenId} AND UploadedByUserId = {userId}
                  AND ReviewId IS NULL AND Status = {(int)ReviewPhotoStatus.Ready}
                """,
                cancellationToken);
        }

        if (attached != photoCount)
        {
            await transaction.RollbackAsync(cancellationToken);
            _dbContext.ChangeTracker.Clear();
            return RedemptionResult.PhotosInvalid;
        }

        await transaction.CommitAsync(cancellationToken);
        return RedemptionResult.Redeemed;
    }

    // InMemory has no transactions or raw SQL: the same conditions on tracked entities, one SaveChanges.
    private async Task<RedemptionResult> RedeemInMemoryAsync(
        VisitToken token,
        Review review,
        IReadOnlyList<Guid> photoIds,
        CancellationToken cancellationToken)
    {
        if (token.RedeemedAt is not null || token.RevokedAt is not null)
        {
            return RedemptionResult.AlreadyRedeemed;
        }

        if (token.PhotoClaimedByUserId is { } claimant && claimant != review.UserId)
        {
            return RedemptionResult.TokenClaimed;
        }

        var photos = await _dbContext.ReviewPhotos
            .Where(photo => photoIds.Contains(photo.Id))
            .ToListAsync(cancellationToken);
        var valid = photos.Count == photoIds.Count
            && photos.All(photo => photo.VisitTokenId == token.Id
                && photo.UploadedByUserId == review.UserId
                && photo.ReviewId == null
                && photo.Status == ReviewPhotoStatus.Ready)
            && token.PhotoSlotsUsed >= photoIds.Count;
        if (!valid)
        {
            foreach (var photo in photos)
            {
                _dbContext.Entry(photo).State = EntityState.Detached;
            }

            return RedemptionResult.PhotosInvalid;
        }

        try
        {
            token.RedeemedAt = review.CreatedAt;
            token.RedeemedByUserId = review.UserId;
            token.ConcurrencyStamp = Guid.NewGuid();
            token.PhotoSlotsUsed -= photoIds.Count;
            for (var position = 0; position < photoIds.Count; position++)
            {
                var photo = photos.Single(item => item.Id == photoIds[position]);
                photo.ReviewId = review.Id;
                photo.SortOrder = position;
                photo.AttachedAt = review.CreatedAt;
                photo.UpdatedAt = review.CreatedAt;
            }

            // Token first, as on SQL Server: a concurrent redemption fails here, before any review exists.
            // InMemory can't roll back a save, so the review is only added once the token save succeeded.
            await _dbContext.SaveChangesAsync(cancellationToken);
            _dbContext.Reviews.Add(review);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return RedemptionResult.Redeemed;
        }
        catch (DbUpdateConcurrencyException)
        {
            _dbContext.ChangeTracker.Clear();
            return RedemptionResult.AlreadyRedeemed;
        }
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };
}
