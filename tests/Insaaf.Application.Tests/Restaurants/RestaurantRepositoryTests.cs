using Insaaf.Domain.Entities;
using Insaaf.Domain.Enums;
using Insaaf.Domain.Repositories;
using Insaaf.Infrastructure.Persistence;
using Insaaf.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Insaaf.Application.Tests.Restaurants;

public sealed class RestaurantRepositoryTests
{
    [Fact]
    public async Task SearchAsync_ExcludesInactiveAndAppliesFilters()
    {
        await using var context = CreateContext();
        var lebanese = new Category { Id = Guid.NewGuid(), Name = "لبناني", Slug = "lebanese" };
        var burger = new Category { Id = Guid.NewGuid(), Name = "برجر", Slug = "burger" };
        var unique = Guid.NewGuid().ToString("N")[..8];

        var activeLebanese = CreateRestaurant($"سفير-{unique}", "وصف لبناني", PriceLevel.Moderate, true);
        var activeBurger = CreateRestaurant($"برجر-{unique}", "برجر لحم", PriceLevel.Budget, true);
        var inactive = CreateRestaurant($"مغلق-{unique}", "غير ظاهر", PriceLevel.Budget, false);

        Link(activeLebanese, lebanese);
        Link(activeBurger, burger);
        Link(inactive, lebanese);

        context.Categories.AddRange(lebanese, burger);
        context.Restaurants.AddRange(activeLebanese, activeBurger, inactive);
        await context.SaveChangesAsync();

        var repository = new RestaurantRepository(context);

        var bySearch = await repository.SearchAsync(new RestaurantSearchCriteria { Search = unique });
        Assert.Equal(2, bySearch.TotalCount);
        Assert.DoesNotContain(bySearch.Items, item => item.Id == inactive.Id);

        var byCategory = await repository.SearchAsync(new RestaurantSearchCriteria
        {
            Search = unique,
            CategorySlug = "lebanese",
        });
        Assert.Single(byCategory.Items);
        Assert.Equal(activeLebanese.Id, byCategory.Items[0].Id);

        var byPrice = await repository.SearchAsync(new RestaurantSearchCriteria
        {
            Search = unique,
            PriceLevel = PriceLevel.Budget,
        });
        Assert.Single(byPrice.Items);
        Assert.Equal(activeBurger.Id, byPrice.Items[0].Id);

        var byMinRating = await repository.SearchAsync(new RestaurantSearchCriteria { MinRating = 4 });
        Assert.Empty(byMinRating.Items);
        Assert.Equal(0, byMinRating.TotalCount);
    }

    [Fact]
    public async Task SearchAsync_MatchesDescriptionAndCityCaseInsensitively()
    {
        await using var context = CreateContext();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var amman = CreateRestaurant("مطعم أول", $"Fresh GRILL {unique}", PriceLevel.Moderate, true);
        var otherCity = CreateRestaurant("مطعم ثاني", $"fresh grill {unique}", PriceLevel.Moderate, true);
        otherCity.City = "Irbid";
        context.Restaurants.AddRange(amman, otherCity);
        await context.SaveChangesAsync();

        var repository = new RestaurantRepository(context);

        var byDescription = await repository.SearchAsync(new RestaurantSearchCriteria { Search = $"grill {unique}" });
        Assert.Equal(2, byDescription.TotalCount);

        var byCity = await repository.SearchAsync(new RestaurantSearchCriteria
        {
            Search = unique,
            City = "IRBID",
        });
        Assert.Single(byCity.Items);
        Assert.Equal(otherCity.Id, byCity.Items[0].Id);
    }

    [Fact]
    public async Task SearchAsync_PaginatesOrderedByName()
    {
        await using var context = CreateContext();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var names = new[] { "e", "c", "a", "d", "b" }.Select(letter => $"{letter}-{unique}").ToList();
        context.Restaurants.AddRange(names.Select(name => CreateRestaurant(name, "وصف", PriceLevel.Budget, true)));
        await context.SaveChangesAsync();

        var repository = new RestaurantRepository(context);

        var secondPage = await repository.SearchAsync(new RestaurantSearchCriteria
        {
            Search = unique,
            Page = 2,
            PageSize = 2,
        });

        Assert.Equal(5, secondPage.TotalCount);
        Assert.Equal(2, secondPage.Page);
        Assert.Equal(2, secondPage.PageSize);
        Assert.Equal([$"c-{unique}", $"d-{unique}"], secondPage.Items.Select(item => item.Name));
    }

    [Fact]
    public async Task SearchAsync_OpenAt_MatchesRuleIncludingPastMidnight()
    {
        await using var context = CreateContext();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var lunch = CreateRestaurant($"lunch-{unique}", "وصف", PriceLevel.Budget, true);
        lunch.TimeZoneId = "UTC";
        lunch.OpeningHours.Add(Hours(DayOfWeek.Sunday, 12, 23));
        var lateNight = CreateRestaurant($"late-{unique}", "وصف", PriceLevel.Budget, true);
        lateNight.TimeZoneId = "UTC";
        lateNight.OpeningHours.Add(Hours(DayOfWeek.Saturday, 10, 1));
        var noTimeZone = CreateRestaurant($"unknown-{unique}", "وصف", PriceLevel.Budget, true);
        noTimeZone.OpeningHours.Add(Hours(DayOfWeek.Sunday, 0, 23));
        context.Restaurants.AddRange(lunch, lateNight, noTimeZone);
        await context.SaveChangesAsync();

        var repository = new RestaurantRepository(context);

        var afterMidnight = await repository.SearchAsync(new RestaurantSearchCriteria
        {
            Search = unique,
            OpenAt = [new LocalTimeInZone("UTC", DayOfWeek.Sunday, new TimeOnly(0, 30))],
        });
        Assert.Equal(lateNight.Id, Assert.Single(afterMidnight.Items).Id);

        var afternoon = await repository.SearchAsync(new RestaurantSearchCriteria
        {
            Search = unique,
            OpenAt = [new LocalTimeInZone("UTC", DayOfWeek.Sunday, new TimeOnly(13, 0))],
        });
        Assert.Equal(lunch.Id, Assert.Single(afternoon.Items).Id);

        var noZones = await repository.SearchAsync(new RestaurantSearchCriteria { Search = unique, OpenAt = [] });
        Assert.Empty(noZones.Items);

        Assert.Equal(["UTC"], await repository.GetTimeZoneIdsAsync());
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_ForInactiveRestaurant()
    {
        await using var context = CreateContext();
        var inactive = CreateRestaurant("غير نشط", "وصف", PriceLevel.Budget, false);
        context.Restaurants.Add(inactive);
        await context.SaveChangesAsync();

        var repository = new RestaurantRepository(context);
        var result = await repository.GetByIdAsync(inactive.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsDetailProjection_ForActiveRestaurant()
    {
        await using var context = CreateContext();
        var restaurant = CreateRestaurant("تفاصيل", "وصف كامل", PriceLevel.Upscale, true);
        restaurant.Phone = "+962 6 555 0000";
        context.Restaurants.Add(restaurant);
        await context.SaveChangesAsync();

        var repository = new RestaurantRepository(context);
        var result = await repository.GetByIdAsync(restaurant.Id);

        Assert.NotNull(result);
        Assert.Equal("تفاصيل", result.Name);
        Assert.Equal("وصف كامل", result.Description);
        Assert.Null(result.AverageRating);
        Assert.Equal(0, result.ReviewCount);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"restaurants-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static Restaurant CreateRestaurant(string name, string description, PriceLevel price, bool isActive) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            City = "عمّان",
            Address = "جبل عمّان",
            Latitude = 31.95,
            Longitude = 35.92,
            PriceLevel = price,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow,
        };

    private static RestaurantOpeningHours Hours(DayOfWeek day, int opensHour, int closesHour) =>
        new()
        {
            Id = Guid.NewGuid(),
            DayOfWeek = day,
            OpensAt = new TimeOnly(opensHour, 0),
            ClosesAt = new TimeOnly(closesHour, 0),
        };

    private static void Link(Restaurant restaurant, Category category)
    {
        restaurant.RestaurantCategories.Add(new RestaurantCategory
        {
            Restaurant = restaurant,
            Category = category,
        });
    }
}
