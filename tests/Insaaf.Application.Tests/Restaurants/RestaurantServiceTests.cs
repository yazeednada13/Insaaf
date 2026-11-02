using Insaaf.Application.Auth.Abstractions;
using Insaaf.Application.Restaurants;
using Insaaf.Application.Restaurants.Dtos;
using Insaaf.Domain.Enums;
using Insaaf.Domain.Repositories;
using Insaaf.Domain.Repositories.Projections;
using Moq;

namespace Insaaf.Application.Tests.Restaurants;

public sealed class RestaurantServiceTests
{
    // Sunday 2026-11-01 13:00 UTC.
    private static readonly DateTime SundayAfternoonUtc = new(2026, 11, 1, 13, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IRestaurantRepository> _repository = new();
    private readonly Mock<IDateTimeProvider> _clock = new();

    public RestaurantServiceTests()
    {
        _clock.Setup(clock => clock.UtcNow).Returns(SundayAfternoonUtc);
    }

    [Fact]
    public async Task SearchAsync_ThrowsValidationError_WhenPageIsInvalid()
    {
        var service = CreateService();

        var exception = await Assert.ThrowsAsync<RestaurantException>(() =>
            service.SearchAsync(new RestaurantSearchQuery(Page: 0)));

        Assert.Equal("validation_error", exception.Code);
        _repository.Verify(
            repo => repo.SearchAsync(It.IsAny<RestaurantSearchCriteria>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SearchAsync_MapsRepositoryResult()
    {
        var restaurantId = Guid.NewGuid();
        _repository
            .Setup(repo => repo.SearchAsync(It.IsAny<RestaurantSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<RestaurantListItemProjection>
            {
                Items =
                [
                    new RestaurantListItemProjection(
                        restaurantId,
                        "مطعم السفير",
                        "عمّان",
                        "جبل عمّان",
                        31.95,
                        35.92,
                        PriceLevel.Moderate,
                        null,
                        [new CategoryProjection(Guid.NewGuid(), "لبناني", "lebanese")],
                        null,
                        0,
                        "UTC",
                        [new OpeningHoursProjection(DayOfWeek.Sunday, new TimeOnly(12, 0), new TimeOnly(23, 0))]),
                ],
                Page = 1,
                PageSize = 20,
                TotalCount = 1,
            });

        var service = CreateService();
        var result = await service.SearchAsync(new RestaurantSearchQuery(Search: "سفير"));

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("مطعم السفير", result.Items[0].Name);
        Assert.Null(result.Items[0].AverageRating);
        Assert.Equal(0, result.Items[0].ReviewCount);
        Assert.True(result.Items[0].IsOpen);
    }

    [Fact]
    public async Task SearchAsync_PassesLocalTimePerZone_WhenOpenNowIsRequested()
    {
        RestaurantSearchCriteria? captured = null;
        _repository
            .Setup(repo => repo.GetTimeZoneIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(["UTC", "Not/AZone"]);
        _repository
            .Setup(repo => repo.SearchAsync(It.IsAny<RestaurantSearchCriteria>(), It.IsAny<CancellationToken>()))
            .Callback<RestaurantSearchCriteria, CancellationToken>((criteria, _) => captured = criteria)
            .ReturnsAsync(new PagedResult<RestaurantListItemProjection>
            {
                Items = [],
                Page = 1,
                PageSize = 20,
                TotalCount = 0,
            });

        await CreateService().SearchAsync(new RestaurantSearchQuery(OpenNow: true));

        Assert.NotNull(captured?.OpenAt);
        var zone = Assert.Single(captured.OpenAt);
        Assert.Equal(new LocalTimeInZone("UTC", DayOfWeek.Sunday, new TimeOnly(13, 0)), zone);
    }

    [Fact]
    public async Task SearchAsync_DoesNotFilterOpeningHours_WhenOpenNowIsNotRequested()
    {
        RestaurantSearchCriteria? captured = null;
        _repository
            .Setup(repo => repo.SearchAsync(It.IsAny<RestaurantSearchCriteria>(), It.IsAny<CancellationToken>()))
            .Callback<RestaurantSearchCriteria, CancellationToken>((criteria, _) => captured = criteria)
            .ReturnsAsync(new PagedResult<RestaurantListItemProjection>
            {
                Items = [],
                Page = 1,
                PageSize = 20,
                TotalCount = 0,
            });

        await CreateService().SearchAsync(new RestaurantSearchQuery());

        Assert.Null(captured?.OpenAt);
        _repository.Verify(repo => repo.GetTimeZoneIdsAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsOpeningStatusAndTodayHours()
    {
        var id = Guid.NewGuid();
        _repository
            .Setup(repo => repo.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RestaurantDetailProjection(
                id,
                "مطعم",
                "وصف",
                "عمّان",
                null,
                31.95,
                35.92,
                PriceLevel.Budget,
                null,
                null,
                [],
                null,
                0,
                "UTC",
                [new OpeningHoursProjection(DayOfWeek.Sunday, new TimeOnly(10, 0), new TimeOnly(1, 0))],
                null));

        var result = await CreateService().GetByIdAsync(id);

        Assert.True(result.IsOpen);
        Assert.Equal("10:00 ص — 1:00 ص", result.TodayHours);
    }

    [Fact]
    public async Task GetByIdAsync_RoundsAverageAndBreakdown()
    {
        var id = Guid.NewGuid();
        _repository
            .Setup(repo => repo.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RestaurantDetailProjection(
                id,
                "مطعم",
                "وصف",
                "عمّان",
                null,
                31.95,
                35.92,
                PriceLevel.Budget,
                null,
                null,
                [],
                4.25m,
                4,
                null,
                [],
                new RatingBreakdownProjection(4.666666m, 3.333333m, 5m, 4.05m)));

        var result = await CreateService().GetByIdAsync(id);

        Assert.Equal(4.3m, result.AverageRating);
        Assert.Equal(4, result.ReviewCount);
        Assert.Equal(new RatingBreakdownDto(4.7m, 3.3m, 5m, 4.1m), result.RatingBreakdown);
    }

    [Fact]
    public async Task GetByIdAsync_ThrowsNotFound_WhenRepositoryReturnsNull()
    {
        var id = Guid.NewGuid();
        _repository
            .Setup(repo => repo.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RestaurantDetailProjection?)null);

        var service = CreateService();

        var exception = await Assert.ThrowsAsync<RestaurantException>(() => service.GetByIdAsync(id));

        Assert.Equal("not_found", exception.Code);
        Assert.Equal(RestaurantStatusCodes.NotFound, exception.StatusCode);
    }

    private RestaurantService CreateService() => new(_repository.Object, _clock.Object);
}
