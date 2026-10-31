using Insaaf.Application.Restaurants.Dtos;
using Insaaf.Application.Restaurants.Validation;

namespace Insaaf.Application.Tests.Restaurants;

public sealed class RestaurantQueryValidatorTests
{
    [Fact]
    public void Validate_ReturnsNoErrors_WhenQueryIsValid()
    {
        var errors = RestaurantQueryValidator.Validate(new RestaurantSearchQuery(
            Search: "سفير",
            City: "عمّان",
            MinRating: 4,
            PriceLevel: 2,
            Category: "lebanese",
            Page: 1,
            PageSize: 20));

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_ReturnsErrors_WhenPagingAndFiltersAreInvalid()
    {
        var errors = RestaurantQueryValidator.Validate(new RestaurantSearchQuery(
            Search: new string('a', 101),
            City: new string('b', 101),
            MinRating: 6,
            PriceLevel: 9,
            Category: "   ",
            Page: 0,
            PageSize: 0));

        Assert.Contains(errors, error => error.Contains("Page"));
        Assert.Contains(errors, error => error.Contains("Page size"));
        Assert.Contains(errors, error => error.Contains("Minimum rating"));
        Assert.Contains(errors, error => error.Contains("Price level"));
        Assert.Contains(errors, error => error.Contains("Search"));
        Assert.Contains(errors, error => error.Contains("City"));
        Assert.Contains(errors, error => error.Contains("Category"));
    }
}
