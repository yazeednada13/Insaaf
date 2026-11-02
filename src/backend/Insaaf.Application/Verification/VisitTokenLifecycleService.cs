using Insaaf.Application.Auth.Abstractions;
using Insaaf.Application.Verification.Dtos;
using Insaaf.Domain.Repositories;

namespace Insaaf.Application.Verification;

public interface IVisitTokenLifecycleService
{
    Task<RevokedVisitTokensDto> RevokeUnusedAsync(
        IssuancePrincipal principal,
        Guid restaurantId,
        Guid? issuanceId,
        CancellationToken cancellationToken = default);

    Task<VisitTokenSummaryDto> GetSummaryAsync(
        IssuancePrincipal principal,
        Guid restaurantId,
        CancellationToken cancellationToken = default);
}

/// <summary>Revocation (rotation) and counts, channel-independent and scope-checked like issuance.</summary>
public sealed class VisitTokenLifecycleService : IVisitTokenLifecycleService
{
    private readonly IVisitTokenRepository _visitTokens;
    private readonly IRestaurantRepository _restaurants;
    private readonly IDateTimeProvider _clock;

    public VisitTokenLifecycleService(
        IVisitTokenRepository visitTokens,
        IRestaurantRepository restaurants,
        IDateTimeProvider clock)
    {
        _visitTokens = visitTokens;
        _restaurants = restaurants;
        _clock = clock;
    }

    public async Task<RevokedVisitTokensDto> RevokeUnusedAsync(
        IssuancePrincipal principal,
        Guid restaurantId,
        Guid? issuanceId,
        CancellationToken cancellationToken = default)
    {
        await EnsureAccessAsync(principal, restaurantId, cancellationToken);
        var revoked = await _visitTokens.RevokeUnusedAsync(restaurantId, issuanceId, _clock.UtcNow, cancellationToken);
        return new RevokedVisitTokensDto(revoked);
    }

    public async Task<VisitTokenSummaryDto> GetSummaryAsync(
        IssuancePrincipal principal,
        Guid restaurantId,
        CancellationToken cancellationToken = default)
    {
        await EnsureAccessAsync(principal, restaurantId, cancellationToken);
        var summary = await _visitTokens.GetSummaryAsync(restaurantId, _clock.UtcNow, cancellationToken);
        return new VisitTokenSummaryDto(summary.Active, summary.Redeemed, summary.Expired, summary.Revoked);
    }

    private async Task EnsureAccessAsync(
        IssuancePrincipal principal,
        Guid restaurantId,
        CancellationToken cancellationToken)
    {
        if (!principal.CanActFor(restaurantId))
        {
            throw new VerificationException(
                "issuance_forbidden",
                "Not allowed to manage visit tokens for this restaurant.",
                VerificationStatusCodes.Forbidden);
        }

        if (await _restaurants.GetPartnershipAsync(restaurantId, cancellationToken) is null)
        {
            throw new VerificationException("not_found", "Restaurant was not found.", VerificationStatusCodes.NotFound);
        }
    }
}
