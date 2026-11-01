using Insaaf.Application.Restaurants;
using Insaaf.Application.Reviews.Dtos;
using Insaaf.Application.Reviews.Validation;
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

    private static ReviewListItemDto ToDto(ReviewListItemProjection review) =>
        new(
            review.Id,
            review.Rating,
            review.Text,
            // datetime2 comes back with Kind=Unspecified; all stored timestamps are UTC.
            DateTime.SpecifyKind(review.CreatedAt, DateTimeKind.Utc),
            review.IsVerified,
            new ReviewerDto(review.ReviewerId, review.ReviewerFullName, review.ReviewerUserName));
}
