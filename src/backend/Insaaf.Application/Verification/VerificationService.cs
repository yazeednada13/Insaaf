using Insaaf.Application.Auth.Abstractions;
using Insaaf.Application.Reviews;
using Insaaf.Application.Verification.Abstractions;
using Insaaf.Application.Verification.Dtos;
using Insaaf.Application.Verification.Validation;
using Insaaf.Domain.Entities;
using Insaaf.Domain.Enums;
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
    public const int MaxPhotos = 5;

    private readonly IVisitTokenRepository _visitTokens;
    private readonly IRestaurantRepository _restaurants;
    private readonly IVisitTokenCodec _codec;
    private readonly IDateTimeProvider _clock;
    private readonly IReviewPhotoRepository _photos;

    public VerificationService(
        IVisitTokenRepository visitTokens,
        IRestaurantRepository restaurants,
        IVisitTokenCodec codec,
        IDateTimeProvider clock,
        IReviewPhotoRepository photos)
    {
        _visitTokens = visitTokens;
        _restaurants = restaurants;
        _codec = codec;
        _clock = clock;
        _photos = photos;
    }

    public async Task<ResolvedVisitDto> ResolveAsync(
        ResolveVisitRequest request,
        CancellationToken cancellationToken = default)
    {
        var token = await GetRedeemableTokenAsync(_visitTokens, _codec, request.Token, _clock.UtcNow, cancellationToken);

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

        var photoIds = request.PhotoIds ?? [];
        if (photoIds.Count > MaxPhotos || photoIds.Distinct().Count() != photoIds.Count || photoIds.Contains(Guid.Empty))
        {
            throw new VerificationException(
                "validation_error",
                $"Up to {MaxPhotos} distinct photo ids are allowed.");
        }

        var now = _clock.UtcNow;
        var token = await GetRedeemableTokenAsync(_visitTokens, _codec, request.Token, now, cancellationToken);

        // Clear errors before the transaction. The guarantee is the redeem statement itself (Phase 9A plan section 3).
        if (token.PhotoClaimedByUserId is { } claimant && claimant != userId)
        {
            throw TokenClaimed();
        }

        if (photoIds.Count > 0)
        {
            var photos = await _photos.ListByIdsAsync(photoIds, cancellationToken);
            if (photos.Count != photoIds.Count
                || photos.Any(photo => photo.VisitTokenId != token.Id
                    || photo.UploadedByUserId != userId
                    || photo.ReviewId is not null
                    || photo.Status != ReviewPhotoStatus.Ready))
            {
                throw InvalidPhoto();
            }
        }

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

        RedemptionResult result;
        try
        {
            result = await _visitTokens.RedeemWithReviewAsync(token, review, photoIds, cancellationToken);
        }
        catch (RepositoryBusyException)
        {
            throw Busy();
        }

        switch (result)
        {
            case RedemptionResult.AlreadyRedeemed:
                throw UsedToken();
            case RedemptionResult.TokenClaimed:
                throw TokenClaimed();
            case RedemptionResult.PhotosInvalid:
                throw InvalidPhoto();
        }

        return new VerifiedReviewCreatedDto(review.Id, review.RestaurantId);
    }

    /// <summary>
    /// The token behind a scanned payload, if it can still be redeemed. Shared with photo uploads, which
    /// accept exactly the tokens a review could be created with (Phase 9A).
    /// </summary>
    internal static async Task<VisitToken> GetRedeemableTokenAsync(
        IVisitTokenRepository visitTokens,
        IVisitTokenCodec codec,
        string? input,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (!VisitPayload.TryExtractToken(input, out var rawToken))
        {
            throw InvalidToken();
        }

        var token = await visitTokens.FindByHashAsync(codec.Hash(rawToken), cancellationToken)
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

    internal static VerificationException InvalidToken() =>
        new("token_invalid", "The visit token is not valid.");

    internal static VerificationException UsedToken() =>
        new("token_used", "The visit token has already been used.", VerificationStatusCodes.Conflict);

    internal static VerificationException TokenClaimed() =>
        new("token_claimed", "This visit token is in use by another account.", VerificationStatusCodes.Forbidden);

    internal static VerificationException InvalidPhoto() =>
        new("invalid_photo", "One or more photos can't be attached to this review.");

    internal static VerificationException Busy() =>
        new("try_again", "Please try again.", VerificationStatusCodes.ServiceUnavailable);
}
