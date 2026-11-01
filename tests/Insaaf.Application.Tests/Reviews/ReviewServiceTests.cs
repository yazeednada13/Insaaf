using Insaaf.Application.Restaurants;
using Insaaf.Application.Reviews;
using Insaaf.Application.Reviews.Dtos;
using Insaaf.Application.Reviews.Validation;
using Insaaf.Domain.Repositories;
using Insaaf.Domain.Repositories.Projections;
using Moq;

namespace Insaaf.Application.Tests.Reviews;

public sealed class ReviewServiceTests
{
    private readonly Mock<IReviewRepository> _reviews = new();
    private readonly Mock<IRestaurantRepository> _restaurants = new();

    [Theory]
    [InlineData(null, ReviewSort.Newest)]
    [InlineData("", ReviewSort.Newest)]
    [InlineData("newest", ReviewSort.Newest)]
    [InlineData("HIGHEST", ReviewSort.Highest)]
    [InlineData(" lowest ", ReviewSort.Lowest)]
    public void Validator_AcceptsKnownSorts(string? sort, ReviewSort expected)
    {
        var query = new RestaurantReviewsQuery(Sort: sort);

        Assert.Empty(ReviewQueryValidator.Validate(query));
        Assert.Equal(expected, ReviewQueryValidator.ParseSort(sort));
    }

    [Fact]
    public void Validator_RejectsInvalidSortAndPaging()
    {
        var errors = ReviewQueryValidator.Validate(new RestaurantReviewsQuery(Sort: "random", Page: 0, PageSize: 51));

        Assert.Contains(errors, error => error.Contains("Sort"));
        Assert.Contains(errors, error => error.Contains("Page must"));
        Assert.Contains(errors, error => error.Contains("Page size"));
    }

    [Theory]
    [InlineData(4.25, 4.3)]
    [InlineData(4.24, 4.2)]
    [InlineData(3.0, 3.0)]
    [InlineData(4.666666, 4.7)]
    public void RatingMath_RoundsHalfAwayFromZero(double input, double expected)
    {
        Assert.Equal((decimal)expected, RatingMath.RoundAverage((decimal)input));
    }

    [Fact]
    public void RatingMath_KeepsNullAverageNull()
    {
        Assert.Null(RatingMath.RoundAverage((decimal?)null));
    }

    [Fact]
    public async Task ListForRestaurantAsync_ThrowsNotFound_WhenRestaurantIsNotActive()
    {
        var restaurantId = Guid.NewGuid();
        _restaurants
            .Setup(repo => repo.IsActiveAsync(restaurantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var exception = await Assert.ThrowsAsync<RestaurantException>(() =>
            CreateService().ListForRestaurantAsync(restaurantId, new RestaurantReviewsQuery()));

        Assert.Equal("not_found", exception.Code);
        Assert.Equal(RestaurantStatusCodes.NotFound, exception.StatusCode);
        _reviews.Verify(
            repo => repo.ListByRestaurantAsync(It.IsAny<ReviewSearchCriteria>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ListForRestaurantAsync_ThrowsValidationError_BeforeQuerying()
    {
        var exception = await Assert.ThrowsAsync<RestaurantException>(() =>
            CreateService().ListForRestaurantAsync(Guid.NewGuid(), new RestaurantReviewsQuery(Sort: "random")));

        Assert.Equal("validation_error", exception.Code);
        _restaurants.Verify(repo => repo.IsActiveAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ListForRestaurantAsync_PassesCriteriaAndMapsUtcTimestamps()
    {
        var restaurantId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        ReviewSearchCriteria? captured = null;
        _restaurants
            .Setup(repo => repo.IsActiveAsync(restaurantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _reviews
            .Setup(repo => repo.ListByRestaurantAsync(It.IsAny<ReviewSearchCriteria>(), It.IsAny<CancellationToken>()))
            .Callback<ReviewSearchCriteria, CancellationToken>((criteria, _) => captured = criteria)
            .ReturnsAsync(new PagedResult<ReviewListItemProjection>
            {
                Items =
                [
                    new ReviewListItemProjection(
                        Guid.NewGuid(),
                        5,
                        "تجربة ممتازة",
                        new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Unspecified),
                        true,
                        reviewerId,
                        "سارة منصور",
                        "sara.eats"),
                ],
                Page = 2,
                PageSize = 10,
                TotalCount = 11,
            });

        var result = await CreateService().ListForRestaurantAsync(
            restaurantId,
            new RestaurantReviewsQuery(Sort: "highest", Page: 2, PageSize: 10));

        Assert.NotNull(captured);
        Assert.Equal(restaurantId, captured.RestaurantId);
        Assert.Equal(ReviewSort.Highest, captured.Sort);
        Assert.Equal(2, captured.Page);
        Assert.Equal(10, captured.PageSize);

        var item = Assert.Single(result.Items);
        Assert.Equal(DateTimeKind.Utc, item.CreatedAt.Kind);
        Assert.Equal(new ReviewerDto(reviewerId, "سارة منصور", "sara.eats"), item.Reviewer);
        Assert.Equal(11, result.TotalCount);
    }

    private ReviewService CreateService() => new(_reviews.Object, _restaurants.Object);
}
