using Insaaf.Application.Restaurants.Dtos;
using Insaaf.Domain.Enums;

namespace Insaaf.Application.Restaurants.Validation;

public static class RestaurantQueryValidator
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 50;
    public const int MaxSearchLength = 100;
    public const int MaxCityLength = 100;

    public static IReadOnlyList<string> Validate(RestaurantSearchQuery query)
    {
        var errors = new List<string>();

        if (query.Page < DefaultPage)
        {
            errors.Add("Page must be at least 1.");
        }

        if (query.PageSize < 1 || query.PageSize > MaxPageSize)
        {
            errors.Add("Page size must be between 1 and 50.");
        }

        if (query.MinRating is not null
            && (query.MinRating < 1.0m || query.MinRating > 5.0m))
        {
            errors.Add("Minimum rating must be between 1.0 and 5.0.");
        }

        if (query.PriceLevel is not null
            && !Enum.IsDefined(typeof(PriceLevel), query.PriceLevel.Value))
        {
            errors.Add("Price level must be between 1 and 4.");
        }

        if (query.Search is not null && query.Search.Trim().Length > MaxSearchLength)
        {
            errors.Add("Search must be at most 100 characters.");
        }

        if (query.City is not null && query.City.Trim().Length > MaxCityLength)
        {
            errors.Add("City must be at most 100 characters.");
        }

        if (query.Category is not null && string.IsNullOrWhiteSpace(query.Category))
        {
            errors.Add("Category slug must not be empty.");
        }

        return errors;
    }
}
