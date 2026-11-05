using Insaaf.Domain.Entities;
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

    public async Task<RedemptionResult> RedeemWithReviewAsync(
        VisitToken token,
        Review review,
        CancellationToken cancellationToken = default)
    {
        // Explicit transaction on relational providers: the token UPDATE and the review INSERT commit
        // together or not at all. (The InMemory test provider has no transactions.)
        await using var transaction = _dbContext.Database.IsRelational()
            ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            // 1) Claim the token. New stamp: the UPDATE only applies if the stamp read during
            //    validation is still current, so a concurrent redemption fails here.
            token.RedeemedAt = review.CreatedAt;
            token.RedeemedByUserId = review.UserId;
            token.ConcurrencyStamp = Guid.NewGuid();
            await _dbContext.SaveChangesAsync(cancellationToken);

            // 2) Only the request that claimed the token creates the review.
            _dbContext.Reviews.Add(review);
            await _dbContext.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return RedemptionResult.Redeemed;
        }
        catch (DbUpdateConcurrencyException)
        {
            // A concurrent request redeemed (or revoked) the token after we validated it.
            _dbContext.ChangeTracker.Clear();
            return RedemptionResult.AlreadyRedeemed;
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            // Last guard: the filtered unique index on Reviews.VisitTokenId (transaction rolls back).
            _dbContext.ChangeTracker.Clear();
            return RedemptionResult.AlreadyRedeemed;
        }
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };
}
