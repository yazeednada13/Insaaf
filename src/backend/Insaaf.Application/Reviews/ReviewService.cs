using Insaaf.Application.Restaurants;
using Insaaf.Application.Reviews.Dtos;
using Insaaf.Application.Reviews.Validation;
using Insaaf.Application.Social;
using Insaaf.Domain.Repositories;
using Insaaf.Domain.Repositories.Projections;

namespace Insaaf.Application.Reviews;

public sealed class ReviewService : IReviewService
{
    private readonly IReviewRepository _reviewRepository;
    private readonly IRestaurantRepository _restaurantRepository;

    public ReviewService(IReviewRepository reviewRepository, IRestaurantRepository restaurantRepository)
    {
        _reviewRepository = reviewRepository;
        _restaurantRepository = restaurantRepository;
    }

    public async Task<ReviewListResultDto> ListForRestaurantAsync(
        Guid restaurantId,
        RestaurantReviewsQuery query,
        Guid viewerUserId,
        CancellationToken cancellationToken = default)
    {
        var validationErrors = ReviewQueryValidator.Validate(query);
        if (validationErrors.Count > 0)
        {
            throw new RestaurantException("validation_error", string.Join(' ', validationErrors));
        }

        // Reviews are a sub-resource: unknown or inactive restaurants are not found.
        if (!await _restaurantRepository.IsActiveAsync(restaurantId, cancellationToken))
        {
            throw new RestaurantException(
                "not_found",
                "Restaurant was not found.",
                RestaurantStatusCodes.NotFound);
        }

        var result = await _reviewRepository.ListByRestaurantAsync(
            new ReviewSearchCriteria
            {
                RestaurantId = restaurantId,
                ViewerUserId = viewerUserId,
                Sort = ReviewQueryValidator.ParseSort(query.Sort),
                Page = query.Page,
                PageSize = query.PageSize,
            },
            cancellationToken);

        return new ReviewListResultDto(
            result.Items.Select(ToDto).ToList(),
            result.Page,
            result.PageSize,
            result.TotalCount);
    }

    public async Task<ReviewDetailDto> GetByIdAsync(
        Guid reviewId,
        Guid viewerUserId,
        CancellationToken cancellationToken = default)
    {
        var review = await _reviewRepository.GetPublicDetailAsync(reviewId, viewerUserId, cancellationToken)
            ?? throw SocialException.ReviewNotFound();

        return new ReviewDetailDto(
            review.Id,
            review.Rating,
            review.FoodRating,
            review.ServiceRating,
            review.ValueRating,
            review.AtmosphereRating,
            review.Text,
            DateTime.SpecifyKind(review.CreatedAt, DateTimeKind.Utc),
            review.IsVerified,
            new ReviewerDto(review.ReviewerId, review.ReviewerFullName, review.ReviewerUserName),
            new ReviewRestaurantDto(
                review.RestaurantId,
                review.RestaurantName,
                review.RestaurantCity,
                review.RestaurantAddress),
            review.LikeCount,
            review.CommentCount,
            review.IsLikedByViewer,
            review.IsReviewerFollowedByViewer);
    }

    private static ReviewListItemDto ToDto(ReviewListItemProjection review) =>
        new(
            review.Id,
            review.Rating,
            review.Text,
            // datetime2 comes back with Kind=Unspecified; all stored timestamps are UTC.
            DateTime.SpecifyKind(review.CreatedAt, DateTimeKind.Utc),
            review.IsVerified,
            new ReviewerDto(review.ReviewerId, review.ReviewerFullName, review.ReviewerUserName),
            review.LikeCount,
            review.CommentCount,
            review.IsLikedByViewer);
}
