using Insaaf.Application.Restaurants.Dtos;

namespace Insaaf.Application.Restaurants;

public interface IRestaurantService
{
    Task<RestaurantListResultDto> SearchAsync(
        RestaurantSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<RestaurantDetailDto> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
