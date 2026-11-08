using Insaaf.Domain.Entities;

namespace Insaaf.Domain.Repositories;

public enum RedemptionResult
{
    Redeemed,

    /// <summary>Another redemption of the same token won the race (or the review already exists).</summary>
    AlreadyRedeemed,

    /// <summary>Another user holds the token's photo claim (Phase 9A).</summary>
    TokenClaimed,

    /// <summary>A submitted photo isn't a Ready, unattached photo of this token and user (Phase 9A).</summary>
    PhotosInvalid,
}

public sealed record VisitTokenSummary(int Active, int Redeemed, int Expired, int Revoked);

/// <summary>Visit-token persistence. Callers never see or pass the raw token, only its hash.</summary>
public interface IVisitTokenRepository
{
    /// <summary>Tracked token with its restaurant, for validation followed by redemption.</summary>
    Task<VisitToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task AddRangeAsync(IReadOnlyCollection<VisitToken> tokens, CancellationToken cancellationToken = default);

    /// <summary>Revokes unredeemed, unrevoked tokens of a restaurant (optionally one issuance). Returns the count.</summary>
    Task<int> RevokeUnusedAsync(
        Guid restaurantId,
        Guid? issuanceId,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    Task<VisitTokenSummary> GetSummaryAsync(
        Guid restaurantId,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the (tracked, already validated) token redeemed and inserts the review and attaches the photos atomically.
    /// The photo claim and slot count are enforced in the same statement that redeems the token.
    /// A concurrent redemption of the same token yields <see cref="RedemptionResult.AlreadyRedeemed"/>.
    /// </summary>
    Task<RedemptionResult> RedeemWithReviewAsync(
        VisitToken token,
        Review review,
        IReadOnlyList<Guid> photoIds,
        CancellationToken cancellationToken = default);
}
