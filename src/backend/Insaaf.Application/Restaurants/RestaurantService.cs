using Insaaf.Application.Restaurants.Dtos;
using Insaaf.Application.Restaurants.Validation;
using Insaaf.Domain.Enums;
using Insaaf.Domain.Repositories;
using Insaaf.Domain.Repositories.Projections;

namespace Insaaf.Application.Restaurants;

public sealed class RestaurantService : IRestaurantService
{
    private readonly IRestaurantRepository _restaurantRepository;

    public RestaurantService(IRestaurantRepository restaurantRepository)
    {
        _restaurantRepository = restaurantRepository;
    }

    public async Task<RestaurantListResultDto> SearchAsync(
        RestaurantSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var validationErrors = RestaurantQueryValidator.Validate(query);
        if (validationErrors.Count > 0)
        {
            throw new RestaurantException("validation_error", string.Join(' ', validationErrors));
        }

        var result = await _restaurantRepository.SearchAsync(
            ToSearchCriteria(query),
            cancellationToken);

        return new RestaurantListResultDto(
            result.Items.Select(ToListItemDto).ToList(),
            result.Page,
            result.PageSize,
            result.TotalCount);
    }

    public async Task<RestaurantDetailDto> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var restaurant = await _restaurantRepository.GetByIdAsync(id, cancellationToken);
        if (restaurant is null)
        {
            throw new RestaurantException(
                "not_found",
                "Restaurant was not found.",
                RestaurantStatusCodes.NotFound);
        }

        return ToDetailDto(restaurant);
    }

    private static RestaurantSearchCriteria ToSearchCriteria(RestaurantSearchQuery query) =>
        new()
        {
            Search = NormalizeOptional(query.Search),
            City = NormalizeOptional(query.City),
            MinRating = query.MinRating,
            PriceLevel = query.PriceLevel is null
                ? null
                : (PriceLevel)query.PriceLevel.Value,
            CategorySlug = NormalizeOptional(query.Category),
            Page = query.Page,
            PageSize = query.PageSize,
        };

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static RestaurantListItemDto ToListItemDto(RestaurantListItemProjection item) =>
        new(
            item.Id,
            item.Name,
            item.City,
            item.Address,
            item.Latitude,
            item.Longitude,
            item.PriceLevel,
            item.ImageUrl,
            item.Categories.Select(ToCategoryDto).ToList(),
            item.AverageRating,
            item.ReviewCount);

    private static RestaurantDetailDto ToDetailDto(RestaurantDetailProjection item) =>
        new(
            item.Id,
            item.Name,
            item.Description,
            item.City,
            item.Address,
            item.Latitude,
            item.Longitude,
            item.PriceLevel,
            item.Phone,
            item.ImageUrl,
            item.Categories.Select(ToCategoryDto).ToList(),
            item.AverageRating,
            item.ReviewCount);

    private static CategoryDto ToCategoryDto(CategoryProjection category) =>
        new(category.Id, category.Name, category.Slug);
}
