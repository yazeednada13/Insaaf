using Insaaf.Application.Restaurants;
using Insaaf.Application.Restaurants.Dtos;
using Insaaf.Domain.Enums;
using Insaaf.Domain.Repositories;
using Insaaf.Domain.Repositories.Projections;
using Moq;

namespace Insaaf.Application.Tests.Restaurants;

public sealed class RestaurantServiceTests
{
    private readonly Mock<IRestaurantRepository> _repository = new();

    [Fact]
    public async Task SearchAsync_ThrowsValidationError_WhenPageIsInvalid()
    {
        var service = new RestaurantService(_repository.Object);

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
                        0),
                ],
                Page = 1,
                PageSize = 20,
                TotalCount = 1,
            });

        var service = new RestaurantService(_repository.Object);
        var result = await service.SearchAsync(new RestaurantSearchQuery(Search: "سفير"));

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("مطعم السفير", result.Items[0].Name);
        Assert.Null(result.Items[0].AverageRating);
        Assert.Equal(0, result.Items[0].ReviewCount);
    }

    [Fact]
    public async Task GetByIdAsync_ThrowsNotFound_WhenRepositoryReturnsNull()
    {
        var id = Guid.NewGuid();
        _repository
            .Setup(repo => repo.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RestaurantDetailProjection?)null);

        var service = new RestaurantService(_repository.Object);

        var exception = await Assert.ThrowsAsync<RestaurantException>(() => service.GetByIdAsync(id));

        Assert.Equal("not_found", exception.Code);
        Assert.Equal(RestaurantStatusCodes.NotFound, exception.StatusCode);
    }
}
