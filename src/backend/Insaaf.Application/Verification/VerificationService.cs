using Insaaf.Application.Auth.Abstractions;
using Insaaf.Application.Reviews;
using Insaaf.Application.Verification.Abstractions;
using Insaaf.Application.Verification.Dtos;
using Insaaf.Application.Verification.Validation;
using Insaaf.Domain.Entities;
using Insaaf.Domain.Repositories;
using Insaaf.Domain.Rules;

namespace Insaaf.Application.Verification;

public interface IVerificationService
{
    /// <summary>Validates a scanned token and returns its restaurant. Does not redeem.</summary>
    Task<ResolvedVisitDto> ResolveAsync(ResolveVisitRequest request, CancellationToken cancellationToken = default);

    /// <summary>Redeems the token and creates the verified review for the token's restaurant.</summary>
    Task<VerifiedReviewCreatedDto> CreateReviewAsync(
        Guid userId,
        CreateVerifiedReviewRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The verified review flow (ADR-004): QR token → server-resolved restaurant → verified review.
/// It never accepts a restaurant from the client and does not care how the token was issued.
/// </summary>
public sealed class VerificationService : IVerificationService
{
    private readonly IVisitTokenRepository _visitTokens;
    private readonly IRestaurantRepository _restaurants;
    private readonly IVisitTokenCodec _codec;
    private readonly IDateTimeProvider _clock;

    public VerificationService(
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

    public async Task<ResolvedVisitDto> ResolveAsync(
        ResolveVisitRequest request,
        CancellationToken cancellationToken = default)
    {
        var token = await GetRedeemableTokenAsync(request.Token, _clock.UtcNow, cancellationToken);

        var restaurant = await _restaurants.GetByIdAsync(token.RestaurantId, cancellationToken)
            ?? throw InvalidToken();

        return new ResolvedVisitDto(new VerifiedRestaurantDto(
            restaurant.Id,
            restaurant.Name,
            restaurant.City,
            restaurant.Address,
            restaurant.ImageUrl,
            RatingMath.RoundAverage(restaurant.AverageRating),
            restaurant.ReviewCount));
    }

    public async Task<VerifiedReviewCreatedDto> CreateReviewAsync(
        Guid userId,
        CreateVerifiedReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationErrors = VerifiedReviewValidator.Validate(request);
        if (validationErrors.Count > 0)
        {
            throw new VerificationException("validation_error", string.Join(' ', validationErrors));
        }

        var now = _clock.UtcNow;
        var token = await GetRedeemableTokenAsync(request.Token, now, cancellationToken);

        var review = new Review
        {
            Id = Guid.NewGuid(),
            // The restaurant is the token's, fixed at issuance — never client input.
            RestaurantId = token.RestaurantId,
            UserId = userId,
            Rating = request.Rating,
            FoodRating = request.FoodRating,
            ServiceRating = request.ServiceRating,
            ValueRating = request.ValueRating,
            AtmosphereRating = request.AtmosphereRating,
            Text = request.Text?.Trim() ?? string.Empty,
            IsVerified = true,
            VisitTokenId = token.Id,
            CreatedAt = now,
        };

        var result = await _visitTokens.RedeemWithReviewAsync(token, review, cancellationToken);
        if (result == RedemptionResult.AlreadyRedeemed)
        {
            throw UsedToken();
        }

        return new VerifiedReviewCreatedDto(review.Id, review.RestaurantId);
    }

    private async Task<VisitToken> GetRedeemableTokenAsync(
        string? input,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (!VisitPayload.TryExtractToken(input, out var rawToken))
        {
            throw InvalidToken();
        }

        var token = await _visitTokens.FindByHashAsync(_codec.Hash(rawToken), cancellationToken)
            ?? throw InvalidToken();

        switch (VisitTokenRules.Evaluate(token, now))
        {
            case VisitTokenState.Used:
                throw UsedToken();
            case VisitTokenState.Expired:
                throw new VerificationException(
                    "token_expired",
                    "The visit token has expired.",
                    VerificationStatusCodes.Gone);
            case VisitTokenState.Revoked:
                throw InvalidToken();
        }

        // Unknown, revoked and non-partner tokens are indistinguishable to the client.
        if (!VisitTokenRules.IsEligibleRestaurant(token.Restaurant.IsActive, token.Restaurant.IsPartner))
        {
            throw InvalidToken();
        }

        return token;
    }

    private static VerificationException InvalidToken() =>
        new("token_invalid", "The visit token is not valid.");

    private static VerificationException UsedToken() =>
        new("token_used", "The visit token has already been used.", VerificationStatusCodes.Conflict);
}
