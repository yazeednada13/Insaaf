using Insaaf.Application.Reviews.Dtos;
using Insaaf.Domain.Repositories;

namespace Insaaf.Application.Reviews.Validation;

public static class ReviewQueryValidator
{
    public const int MaxPageSize = 50;

    private static readonly IReadOnlyDictionary<string, ReviewSort> Sorts =
        new Dictionary<string, ReviewSort>(StringComparer.OrdinalIgnoreCase)
        {
            ["newest"] = ReviewSort.Newest,
            ["highest"] = ReviewSort.Highest,
            ["lowest"] = ReviewSort.Lowest,
        };

    public static IReadOnlyList<string> Validate(RestaurantReviewsQuery query)
    {
        var errors = new List<string>();

        if (query.Page < 1)
        {
            errors.Add("Page must be at least 1.");
        }

        if (query.PageSize < 1 || query.PageSize > MaxPageSize)
        {
            errors.Add("Page size must be between 1 and 50.");
        }

        if (!string.IsNullOrWhiteSpace(query.Sort) && !Sorts.ContainsKey(query.Sort.Trim()))
        {
            errors.Add("Sort must be one of: newest, highest, lowest.");
        }

        return errors;
    }

    /// <summary>Parses a validated sort value; empty means newest.</summary>
    public static ReviewSort ParseSort(string? sort) =>
        string.IsNullOrWhiteSpace(sort) ? ReviewSort.Newest : Sorts[sort.Trim()];
}
