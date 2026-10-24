using Insaaf.Domain.Enums;

namespace Insaaf.Domain.Repositories;

public sealed class RestaurantSearchCriteria
{
    public string? Search { get; init; }

    public string? City { get; init; }

    public decimal? MinRating { get; init; }

    public PriceLevel? PriceLevel { get; init; }

    public string? CategorySlug { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}
