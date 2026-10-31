using Insaaf.Domain.Enums;

namespace Insaaf.Domain.Repositories;

public sealed class RestaurantSearchCriteria
{
    public string? Search { get; init; }

    public string? City { get; init; }

    public decimal? MinRating { get; init; }

    public PriceLevel? PriceLevel { get; init; }

    public string? CategorySlug { get; init; }

    /// <summary>
    /// openNow filter: the current local day/time for each restaurant time zone. When set, only
    /// restaurants open at their zone's local time match; an empty list matches nothing.
    /// </summary>
    public IReadOnlyList<LocalTimeInZone>? OpenAt { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}

public sealed record LocalTimeInZone(string TimeZoneId, DayOfWeek Day, TimeOnly Time);
