using Insaaf.Application.Auth.Abstractions;
using Insaaf.Application.Verification.Abstractions;
using Insaaf.Application.Verification.Dtos;
using Insaaf.Domain.Entities;
using Insaaf.Domain.Repositories;
using Insaaf.Domain.Rules;

namespace Insaaf.Application.Verification;

public interface IVisitTokenIssuer
{
    Task<IssuedVisitTokensDto> IssueAsync(
        IssuancePrincipal principal,
        VisitTokenIssueRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The single, channel-independent way to create visit tokens. It knows nothing about Admins or batches:
/// the caller supplies an <see cref="IssuancePrincipal"/> established by the server, and this service
/// enforces the principal's restaurant scope and the restaurant's eligibility before creating anything.
/// </summary>
public sealed class VisitTokenIssuer : IVisitTokenIssuer
{
    public const int MaxTokensPerIssuance = 500;

    private readonly IVisitTokenRepository _visitTokens;
    private readonly IRestaurantRepository _restaurants;
    private readonly IVisitTokenCodec _codec;
    private readonly IDateTimeProvider _clock;

    public VisitTokenIssuer(
        IVisitTokenRepository visitTokens,
        IRestaurantRepository restaurants,
        IVisitTokenCodec codec,
        IDateTimeProvider clock)
    {
        _visitTokens = visitTokens;
        _restaurants = restaurants;
        _codec = codec;
        _clock = clock;
    }

    public async Task<IssuedVisitTokensDto> IssueAsync(
        IssuancePrincipal principal,
        VisitTokenIssueRequest request,
        CancellationToken cancellationToken = default)
    {
        // Scope first: a restricted principal can never act for another restaurant, whatever the request says.
        if (!principal.CanActFor(request.RestaurantId))
        {
            throw new VerificationException(
                "issuance_forbidden",
                "Not allowed to issue visit tokens for this restaurant.",
                VerificationStatusCodes.Forbidden);
        }

        var now = _clock.UtcNow;
        if (request.Count < 1 || request.Count > MaxTokensPerIssuance)
        {
            throw new VerificationException("validation_error", $"Count must be between 1 and {MaxTokensPerIssuance}.");
        }

        if (request.ExpiresAt <= now)
        {
            throw new VerificationException("validation_error", "Expiry must be in the future.");
        }

        var restaurant = await _restaurants.GetPartnershipAsync(request.RestaurantId, cancellationToken)
            ?? throw new VerificationException("not_found", "Restaurant was not found.", VerificationStatusCodes.NotFound);

        if (!VisitTokenRules.IsEligibleRestaurant(restaurant.IsActive, restaurant.IsPartner))
        {
            throw new VerificationException(
                "restaurant_not_partner",
                "Visit tokens can only be issued for active partner restaurants.",
                VerificationStatusCodes.Conflict);
        }

        var issuanceId = Guid.NewGuid();
        var rawTokens = new List<string>(request.Count);
        var tokens = new List<VisitToken>(request.Count);
        for (var i = 0; i < request.Count; i++)
        {
            var raw = _codec.GenerateToken();
            rawTokens.Add(raw);
            tokens.Add(new VisitToken
            {
                Id = Guid.NewGuid(),
                RestaurantId = request.RestaurantId,
                IssuanceId = issuanceId,
                // Channel and issuer come from the principal, never from the request.
                IssuanceChannel = principal.Channel,
                IssuedByUserId = principal.UserId,
                TokenHash = _codec.Hash(raw),
                IssuedAt = now,
                ExpiresAt = request.ExpiresAt,
                ConcurrencyStamp = Guid.NewGuid(),
            });
        }

        await _visitTokens.AddRangeAsync(tokens, cancellationToken);

        return new IssuedVisitTokensDto(
            issuanceId,
            request.RestaurantId,
            request.ExpiresAt,
            rawTokens.Select(VisitPayload.Build).ToList());
    }
}
