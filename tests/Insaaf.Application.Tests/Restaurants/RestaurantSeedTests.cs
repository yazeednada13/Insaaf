using Insaaf.Domain.Enums;
using Insaaf.Infrastructure.Persistence;
using Insaaf.Infrastructure.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Insaaf.Application.Tests.Restaurants;

public sealed class RestaurantSeedTests
{
    [Fact]
    public async Task SeedAsync_IsIdempotent_WhenRunTwice()
    {
        await using var provider = CreateProvider();

        await RestaurantSeed.SeedAsync(provider);
        await RestaurantSeed.SeedAsync(provider);

        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(12, await context.Restaurants.CountAsync());
        Assert.Equal(10, await context.Categories.CountAsync());
    }

    [Fact]
    public async Task SeedAsync_CoversPilotDatasetTargets()
    {
        await using var provider = CreateProvider();

        await RestaurantSeed.SeedAsync(provider);

        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var restaurants = await context.Restaurants
            .Include(restaurant => restaurant.RestaurantCategories)
            .ToListAsync();

        Assert.Equal(
            [PriceLevel.Budget, PriceLevel.Moderate, PriceLevel.Upscale, PriceLevel.Premium],
            restaurants.Select(restaurant => restaurant.PriceLevel).Distinct().Order());
        Assert.Single(restaurants, restaurant => !restaurant.IsActive);
        Assert.All(restaurants, restaurant => Assert.Equal("عمّان", restaurant.City));
        Assert.All(restaurants, restaurant => Assert.NotEmpty(restaurant.RestaurantCategories));
        Assert.All(restaurants, restaurant =>
        {
            Assert.InRange(restaurant.Latitude, 31.8, 32.1);
            Assert.InRange(restaurant.Longitude, 35.7, 36.1);
        });
        Assert.Equal(
            restaurants.Count,
            restaurants.Select(restaurant => (restaurant.Latitude, restaurant.Longitude)).Distinct().Count());
    }

    private static ServiceProvider CreateProvider()
    {
        var databaseName = $"restaurant-seed-{Guid.NewGuid():N}";
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(databaseName));
        return services.BuildServiceProvider();
    }
}
