using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Insaaf.Application.Auth.Dtos;
using Insaaf.Application.Common.Models;
using Insaaf.Application.Restaurants.Dtos;
using Insaaf.Application.Reviews.Dtos;
using Insaaf.Domain.Entities;
using Insaaf.Domain.Enums;
using Insaaf.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Insaaf.API.Tests;

public sealed class ReviewEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public ReviewEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Reviews_ReturnsUnauthorizedEnvelope_WhenTokenIsMissing()
    {
        var response = await _client.GetAsync($"/api/v1/restaurants/{Guid.NewGuid()}/reviews");
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<ReviewListResultDto>>(JsonOptions);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(envelope!.Errors!, error => error.Code == "unauthorized");
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("not-a-guid")]
    [InlineData("inactive")]
    public async Task Reviews_ReturnsNotFound_ForUnknownInvalidOrInactiveRestaurant(string kind)
    {
        var auth = await RegisterAsync();
        var id = kind switch
        {
            "unknown" => Guid.NewGuid().ToString(),
            "not-a-guid" => "not-a-guid",
            _ => (await CreateRestaurantAsync(isActive: false)).ToString(),
        };

        var response = await SendAsync($"/api/v1/restaurants/{id}/reviews", auth.AccessToken);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<ReviewListResultDto>>(JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains(envelope!.Errors!, error => error.Code == "not_found");
    }

    [Theory]
    [InlineData("sort=random")]
    [InlineData("pageSize=51")]
    [InlineData("page=abc")]
    public async Task Reviews_ReturnsValidationError_ForInvalidQuery(string queryString)
    {
        var auth = await RegisterAsync();
        var restaurantId = await CreateRestaurantAsync(isActive: true);

        var response = await SendAsync($"/api/v1/restaurants/{restaurantId}/reviews?{queryString}", auth.AccessToken);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<ReviewListResultDto>>(JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(envelope!.Errors!, error => error.Code == "validation_error");
    }

    [Fact]
    public async Task Reviews_ReturnsEmptyList_AndDetailHasNoAggregates_WhenRestaurantHasNoReviews()
    {
        var auth = await RegisterAsync();
        var restaurantId = await CreateRestaurantAsync(isActive: true);

        var reviews = await GetReviewsAsync(restaurantId, auth.AccessToken, string.Empty);
        Assert.Empty(reviews.Items);
        Assert.Equal(0, reviews.TotalCount);

        var detail = await GetDetailAsync(restaurantId, auth.AccessToken);
        Assert.Null(detail.AverageRating);
        Assert.Equal(0, detail.ReviewCount);
        Assert.Null(detail.RatingBreakdown);
    }

    [Fact]
    public async Task Reviews_ListsPublicReviewsSortedAndPaged_WithReviewer()
    {
        var auth = await RegisterAsync();
        var restaurantId = await CreateRestaurantAsync(isActive: true);
        await AddReviewsAsync(
            restaurantId,
            auth.User.Id,
            (Rating: 3, DaysAgo: 3, Verified: true),
            (Rating: 5, DaysAgo: 2, Verified: true),
            (Rating: 4, DaysAgo: 1, Verified: true),
            (Rating: 1, DaysAgo: 0, Verified: false));

        var newest = await GetReviewsAsync(restaurantId, auth.AccessToken, "?pageSize=2");
        Assert.Equal(3, newest.TotalCount);
        Assert.Equal([4, 5], newest.Items.Select(item => item.Rating));
        Assert.All(newest.Items, item => Assert.True(item.IsVerified));
        Assert.All(newest.Items, item => Assert.Equal(auth.User.Id, item.Reviewer.Id));
        Assert.All(newest.Items, item => Assert.Equal("Test User", item.Reviewer.FullName));

        var secondPage = await GetReviewsAsync(restaurantId, auth.AccessToken, "?pageSize=2&page=2");
        Assert.Equal([3], secondPage.Items.Select(item => item.Rating));

        var highest = await GetReviewsAsync(restaurantId, auth.AccessToken, "?sort=highest");
        Assert.Equal([5, 4, 3], highest.Items.Select(item => item.Rating));
    }

    [Fact]
    public async Task Reviews_SerializesCreatedAtAsUtc()
    {
        var auth = await RegisterAsync();
        var restaurantId = await CreateRestaurantAsync(isActive: true);
        await AddReviewsAsync(restaurantId, auth.User.Id, (Rating: 5, DaysAgo: 1, Verified: true));

        var response = await SendAsync($"/api/v1/restaurants/{restaurantId}/reviews", auth.AccessToken);
        var json = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(json);
        var createdAt = document.RootElement
            .GetProperty("data").GetProperty("items")[0].GetProperty("createdAt").GetString();
        Assert.EndsWith("Z", createdAt);
    }

    [Fact]
    public async Task Detail_ReturnsRoundedAggregatesFromPublicReviewsOnly()
    {
        var auth = await RegisterAsync();
        var restaurantId = await CreateRestaurantAsync(isActive: true);
        await AddReviewsAsync(
            restaurantId,
            auth.User.Id,
            (Rating: 5, DaysAgo: 2, Verified: true),
            (Rating: 4, DaysAgo: 1, Verified: true),
            (Rating: 4, DaysAgo: 1, Verified: true),
            (Rating: 1, DaysAgo: 0, Verified: false));

        var detail = await GetDetailAsync(restaurantId, auth.AccessToken);

        Assert.Equal(3, detail.ReviewCount);
        Assert.Equal(4.3m, detail.AverageRating);
        Assert.NotNull(detail.RatingBreakdown);
        Assert.Equal(4.0m, detail.RatingBreakdown.Food);
    }

    [Fact]
    public async Task List_MinRating_ReturnsRestaurantsAtOrAboveAverage()
    {
        var auth = await RegisterAsync();
        var restaurantId = await CreateRestaurantAsync(isActive: true, name: $"minrating-{Guid.NewGuid():N}");
        await AddReviewsAsync(restaurantId, auth.User.Id, (Rating: 5, DaysAgo: 1, Verified: true));

        var response = await SendAsync("/api/v1/restaurants?minRating=4.5&pageSize=50", auth.AccessToken);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<RestaurantListResultDto>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(envelope!.Data!.Items, item => item.Id == restaurantId && item.AverageRating == 5m);
        Assert.All(envelope.Data.Items, item => Assert.True(item.AverageRating >= 4.5m));
    }

    private async Task<AuthResponseDto> RegisterAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterRequest($"reviews-{Guid.NewGuid():N}@example.com", "Password1!", "Test User", $"user_{Guid.NewGuid():N}"));
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>(JsonOptions);
        return envelope!.Data!;
    }

    private async Task<Guid> CreateRestaurantAsync(bool isActive, string? name = null)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var restaurant = new Restaurant
        {
            Id = Guid.NewGuid(),
            Name = name ?? $"reviews-{Guid.NewGuid():N}",
            Description = "وصف",
            City = "عمّان",
            Latitude = 31.95,
            Longitude = 35.92,
            PriceLevel = PriceLevel.Moderate,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow,
        };
        context.Restaurants.Add(restaurant);
        await context.SaveChangesAsync();
        return restaurant.Id;
    }

    // Test-only data: Phase 4 has no review creation path; reviews are inserted directly.
    private async Task AddReviewsAsync(
        Guid restaurantId,
        Guid userId,
        params (int Rating, int DaysAgo, bool Verified)[] reviews)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.Reviews.AddRange(reviews.Select(review => new Review
        {
            Id = Guid.NewGuid(),
            RestaurantId = restaurantId,
            UserId = userId,
            Rating = review.Rating,
            FoodRating = 4,
            ServiceRating = 4,
            ValueRating = 4,
            AtmosphereRating = 4,
            Text = "مراجعة اختبارية",
            IsVerified = review.Verified,
            VisitTokenId = review.Verified ? Guid.NewGuid() : null,
            CreatedAt = DateTime.UtcNow.AddDays(-review.DaysAgo),
        }));
        await context.SaveChangesAsync();
    }

    private async Task<ReviewListResultDto> GetReviewsAsync(Guid restaurantId, string token, string queryString)
    {
        var response = await SendAsync($"/api/v1/restaurants/{restaurantId}/reviews{queryString}", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<ReviewListResultDto>>(JsonOptions);
        return envelope!.Data!;
    }

    private async Task<RestaurantDetailDto> GetDetailAsync(Guid restaurantId, string token)
    {
        var response = await SendAsync($"/api/v1/restaurants/{restaurantId}", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<RestaurantDetailDto>>(JsonOptions);
        return envelope!.Data!;
    }

    private Task<HttpResponseMessage> SendAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
