using Insaaf.Domain.Entities;
using Insaaf.Domain.Enums;
using Insaaf.Domain.Repositories;
using Insaaf.Infrastructure.Identity;
using Insaaf.Infrastructure.Persistence;
using Insaaf.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Insaaf.Application.Tests.Reviews;

public sealed class ReviewRepositoryTests
{
    private static readonly DateTime BaseTime = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ListByRestaurantAsync_ReturnsOnlyPublicReviewsOfThatRestaurant_WithReviewer()
    {
        await using var context = CreateContext();
        var (restaurant, other, reviewer) = await SeedAsync(context);
        context.Reviews.AddRange(
            CreateReview(restaurant, reviewer, 5, BaseTime, verified: true),
            CreateReview(restaurant, reviewer, 1, BaseTime.AddDays(1), verified: false),
            CreateReview(other, reviewer, 4, BaseTime, verified: true));
        await context.SaveChangesAsync();

        var result = await new ReviewRepository(context).ListByRestaurantAsync(
            new ReviewSearchCriteria { RestaurantId = restaurant.Id });

        Assert.Equal(1, result.TotalCount);
        var item = Assert.Single(result.Items);
        Assert.Equal(5, item.Rating);
        Assert.True(item.IsVerified);
        Assert.Equal(reviewer.Id, item.ReviewerId);
        Assert.Equal("سارة منصور", item.ReviewerFullName);
        Assert.Equal("sara.eats", item.ReviewerUserName);
    }

    [Theory]
    [InlineData(ReviewSort.Newest, new[] { 3, 5, 5, 1 })]
    [InlineData(ReviewSort.Highest, new[] { 5, 5, 3, 1 })]
    [InlineData(ReviewSort.Lowest, new[] { 1, 3, 5, 5 })]
    public async Task ListByRestaurantAsync_SortsAsRequested(ReviewSort sort, int[] expectedRatings)
    {
        await using var context = CreateContext();
        var (restaurant, _, reviewer) = await SeedAsync(context);
        context.Reviews.AddRange(
            CreateReview(restaurant, reviewer, 1, BaseTime, verified: true),
            CreateReview(restaurant, reviewer, 5, BaseTime.AddDays(1), verified: true),
            CreateReview(restaurant, reviewer, 5, BaseTime.AddDays(2), verified: true),
            CreateReview(restaurant, reviewer, 3, BaseTime.AddDays(3), verified: true));
        await context.SaveChangesAsync();

        var result = await new ReviewRepository(context).ListByRestaurantAsync(
            new ReviewSearchCriteria { RestaurantId = restaurant.Id, Sort = sort });

        Assert.Equal(expectedRatings, result.Items.Select(item => item.Rating));
    }

    [Fact]
    public async Task ListByRestaurantAsync_PaginatesWithStableOrder()
    {
        await using var context = CreateContext();
        var (restaurant, _, reviewer) = await SeedAsync(context);
        // Identical timestamps: the Id tie-breaker keeps pages disjoint and complete.
        var reviews = Enumerable.Range(0, 5)
            .Select(_ => CreateReview(restaurant, reviewer, 4, BaseTime, verified: true))
            .ToList();
        context.Reviews.AddRange(reviews);
        await context.SaveChangesAsync();

        var repository = new ReviewRepository(context);
        var pages = new List<Guid>();
        for (var page = 1; page <= 3; page++)
        {
            var result = await repository.ListByRestaurantAsync(
                new ReviewSearchCriteria { RestaurantId = restaurant.Id, Page = page, PageSize = 2 });
            Assert.Equal(5, result.TotalCount);
            pages.AddRange(result.Items.Select(item => item.Id));
        }

        Assert.Equal(reviews.Select(review => review.Id).Order(), pages.Order());
        Assert.Equal(5, pages.Distinct().Count());
    }

    [Fact]
    public async Task RestaurantAggregates_CountOnlyPublicReviews()
    {
        await using var context = CreateContext();
        var (restaurant, other, reviewer) = await SeedAsync(context);
        context.Reviews.AddRange(
            CreateReview(restaurant, reviewer, 5, BaseTime, verified: true, food: 5, service: 4, value: 3, atmosphere: 5),
            CreateReview(restaurant, reviewer, 4, BaseTime, verified: true, food: 4, service: 4, value: 4, atmosphere: 4),
            CreateReview(restaurant, reviewer, 4, BaseTime, verified: true, food: 4, service: 3, value: 5, atmosphere: 3),
            CreateReview(restaurant, reviewer, 1, BaseTime, verified: false, food: 1, service: 1, value: 1, atmosphere: 1));
        await context.SaveChangesAsync();

        var repository = new RestaurantRepository(context);

        var detail = await repository.GetByIdAsync(restaurant.Id);
        Assert.NotNull(detail);
        Assert.Equal(3, detail.ReviewCount);
        Assert.Equal(13m / 3m, detail.AverageRating!.Value, 6);
        Assert.NotNull(detail.RatingBreakdown);
        Assert.Equal(13m / 3m, detail.RatingBreakdown.Food, 6);
        Assert.Equal(11m / 3m, detail.RatingBreakdown.Service, 6);
        Assert.Equal(4m, detail.RatingBreakdown.Value, 6);
        Assert.Equal(4m, detail.RatingBreakdown.Atmosphere, 6);

        var withoutReviews = await repository.GetByIdAsync(other.Id);
        Assert.NotNull(withoutReviews);
        Assert.Null(withoutReviews.AverageRating);
        Assert.Equal(0, withoutReviews.ReviewCount);
        Assert.Null(withoutReviews.RatingBreakdown);

        var list = await repository.SearchAsync(new RestaurantSearchCriteria { Search = "audit" });
        var listed = list.Items.Single(item => item.Id == restaurant.Id);
        Assert.Equal(3, listed.ReviewCount);
        Assert.Equal(13m / 3m, listed.AverageRating!.Value, 6);
    }

    [Fact]
    public async Task MinRating_UsesPublicAverage_AndExcludesRestaurantsWithoutReviews()
    {
        await using var context = CreateContext();
        var (restaurant, other, reviewer) = await SeedAsync(context);
        context.Reviews.AddRange(
            CreateReview(restaurant, reviewer, 4, BaseTime, verified: true),
            CreateReview(restaurant, reviewer, 5, BaseTime, verified: true),
            // An unverified 5 must not lift the other restaurant into the results.
            CreateReview(other, reviewer, 5, BaseTime, verified: false));
        await context.SaveChangesAsync();

        var repository = new RestaurantRepository(context);

        var atFour = await repository.SearchAsync(new RestaurantSearchCriteria { Search = "audit", MinRating = 4 });
        Assert.Equal(restaurant.Id, Assert.Single(atFour.Items).Id);

        var atFourPointFive = await repository.SearchAsync(new RestaurantSearchCriteria { Search = "audit", MinRating = 4.5m });
        Assert.Equal(restaurant.Id, Assert.Single(atFourPointFive.Items).Id);

        var aboveAverage = await repository.SearchAsync(new RestaurantSearchCriteria { Search = "audit", MinRating = 4.6m });
        Assert.Empty(aboveAverage.Items);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"reviews-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<(Restaurant Restaurant, Restaurant Other, ApplicationUser Reviewer)> SeedAsync(
        AppDbContext context)
    {
        var reviewer = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "sara.eats",
            FullName = "سارة منصور",
            Email = "sara@example.com",
        };
        var restaurant = CreateRestaurant("audit-reviewed");
        var other = CreateRestaurant("audit-other");
        context.Users.Add(reviewer);
        context.Restaurants.AddRange(restaurant, other);
        await context.SaveChangesAsync();
        return (restaurant, other, reviewer);
    }

    private static Restaurant CreateRestaurant(string name) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = "وصف",
            City = "عمّان",
            Latitude = 31.95,
            Longitude = 35.92,
            PriceLevel = PriceLevel.Moderate,
            IsActive = true,
            CreatedAt = BaseTime,
        };

    private static Review CreateReview(
        Restaurant restaurant,
        ApplicationUser reviewer,
        int rating,
        DateTime createdAt,
        bool verified,
        int food = 4,
        int service = 4,
        int value = 4,
        int atmosphere = 4) =>
        new()
        {
            Id = Guid.NewGuid(),
            RestaurantId = restaurant.Id,
            UserId = reviewer.Id,
            Rating = rating,
            FoodRating = food,
            ServiceRating = service,
            ValueRating = value,
            AtmosphereRating = atmosphere,
            Text = "مراجعة اختبارية",
            IsVerified = verified,
            VisitTokenId = verified ? Guid.NewGuid() : null,
            CreatedAt = createdAt,
        };
}
