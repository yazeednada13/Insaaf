using Insaaf.Application.Auth.Abstractions;
using Insaaf.Application.Restaurants.Dtos;
using Insaaf.Application.Restaurants.Validation;
using Insaaf.Application.Reviews;
using Insaaf.Domain.Enums;
using Insaaf.Domain.Repositories;
using Insaaf.Domain.Repositories.Projections;

namespace Insaaf.Application.Restaurants;

public sealed class RestaurantService : IRestaurantService
{
    private readonly IRestaurantRepository _restaurantRepository;
    private readonly IDateTimeProvider _dateTimeProvider;

    public RestaurantService(IRestaurantRepository restaurantRepository, IDateTimeProvider dateTimeProvider)
    {
        _restaurantRepository = restaurantRepository;
        _dateTimeProvider = dateTimeProvider;
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

        var utcNow = _dateTimeProvider.UtcNow;
        IReadOnlyList<LocalTimeInZone>? openAt = null;
        if (query.OpenNow == true)
        {
            var timeZoneIds = await _restaurantRepository.GetTimeZoneIdsAsync(cancellationToken);
            openAt = OpeningHoursCalculator.LocalTimesFor(timeZoneIds, utcNow);
        }

        var result = await _restaurantRepository.SearchAsync(
            ToSearchCriteria(query, openAt),
            cancellationToken);

        return new RestaurantListResultDto(
            result.Items.Select(item => ToListItemDto(item, utcNow)).ToList(),
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

        return ToDetailDto(restaurant, _dateTimeProvider.UtcNow);
    }

    private static RestaurantSearchCriteria ToSearchCriteria(
        RestaurantSearchQuery query,
        IReadOnlyList<LocalTimeInZone>? openAt) =>
        new()
        {
            Search = NormalizeOptional(query.Search),
            City = NormalizeOptional(query.City),
            MinRating = query.MinRating,
            PriceLevel = query.PriceLevel is null
                ? null
                : (PriceLevel)query.PriceLevel.Value,
            CategorySlug = NormalizeOptional(query.Category),
            OpenAt = openAt,
            Page = query.Page,
            PageSize = query.PageSize,
        };

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static RestaurantListItemDto ToListItemDto(RestaurantListItemProjection item, DateTime utcNow) =>
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
            RatingMath.RoundAverage(item.AverageRating),
            item.ReviewCount,
            OpeningHoursCalculator.Evaluate(item.TimeZoneId, item.OpeningHours, utcNow).IsOpen);

    private static RestaurantDetailDto ToDetailDto(RestaurantDetailProjection item, DateTime utcNow)
    {
        var status = OpeningHoursCalculator.Evaluate(item.TimeZoneId, item.OpeningHours, utcNow);

        return new(
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
            RatingMath.RoundAverage(item.AverageRating),
            item.ReviewCount,
            status.IsOpen,
            status.TodayHours,
            item.RatingBreakdown is null ? null : ToRatingBreakdownDto(item.RatingBreakdown));
    }

    private static RatingBreakdownDto ToRatingBreakdownDto(RatingBreakdownProjection breakdown) =>
        new(
            RatingMath.RoundAverage(breakdown.Food),
            RatingMath.RoundAverage(breakdown.Service),
            RatingMath.RoundAverage(breakdown.Value),
            RatingMath.RoundAverage(breakdown.Atmosphere));

    private static CategoryDto ToCategoryDto(CategoryProjection category) =>
        new(category.Id, category.Name, category.Slug);
}
