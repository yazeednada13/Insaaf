using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Insaaf.Application.Common.Models;
using Insaaf.Application.Social.Dtos;

namespace Insaaf.API.Tests;

/// <summary>Notifications, Home and Taste on reviews created through the real QR flow.</summary>
public sealed class Phase8AEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly CustomWebApplicationFactory _factory;
    private readonly SocialApi _api;

    public Phase8AEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _api = new SocialApi(factory, factory.CreateClient());
    }

    [Fact]
    public async Task AllRoutes_RequireAuthentication()
    {
        var client = _factory.CreateClient();
        var id = Guid.NewGuid();

        foreach (var url in new[]
        {
            "/api/v1/notifications",
            "/api/v1/notifications/unread-count",
            "/api/v1/home/suggested",
            "/api/v1/home/trending",
            $"/api/v1/users/{id}/taste",
            $"/api/v1/users/{id}/taste-match",
        })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(url)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/v1/notifications/read-all", null)).StatusCode);
    }

    [Fact]
    public async Task Actions_NotifyTheAuthor_AndReadAllIsPerCaller()
    {
        var (_, reviewId, author) = await _api.CreateVerifiedReviewAsync();
        var authorId = (await ReadAsync<UserProfileDto>(await _api.SendAsync(HttpMethod.Get, "/api/v1/users/me", author))).Id;
        var other = await _api.RegisterAsync();

        await _api.SendAsync(HttpMethod.Put, $"/api/v1/reviews/{reviewId}/like", other);
        await _api.SendAsync(HttpMethod.Post, $"/api/v1/reviews/{reviewId}/comments", other, new { text = "تعليق" });
        await _api.SendAsync(HttpMethod.Put, $"/api/v1/users/{authorId}/follow", other);
        await _api.SendAsync(HttpMethod.Put, $"/api/v1/reviews/{reviewId}/like", author);

        var response = await _api.SendAsync(HttpMethod.Get, "/api/v1/notifications", author);
        var raw = await response.Content.ReadAsStringAsync();
        var list = JsonSerializer.Deserialize<ApiResponse<NotificationListResultDto>>(raw, JsonOptions)!.Data!;

        Assert.Equal(["follow", "comment", "like"], list.Items.Select(item => item.Type));
        Assert.All(list.Items, item => Assert.False(item.IsRead));
        Assert.Equal(reviewId, list.Items[1].ReviewId);
        Assert.DoesNotContain("example.com", raw, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(3, (await ReadAsync<UnreadCountDto>(await _api.SendAsync(HttpMethod.Get, "/api/v1/notifications/unread-count", author))).Count);
        Assert.Empty((await ReadAsync<NotificationListResultDto>(await _api.SendAsync(HttpMethod.Get, "/api/v1/notifications", other))).Items);

        await _api.SendAsync(HttpMethod.Post, "/api/v1/notifications/read-all", author);

        Assert.Equal(0, (await ReadAsync<UnreadCountDto>(await _api.SendAsync(HttpMethod.Get, "/api/v1/notifications/unread-count", author))).Count);
    }

    [Fact]
    public async Task Home_ReturnsActiveRestaurants()
    {
        var user = await _api.RegisterAsync();

        var suggested = await ReadAsync<HomeRestaurantsDto>(await _api.SendAsync(HttpMethod.Get, "/api/v1/home/suggested", user));
        var trending = await ReadAsync<HomeRestaurantsDto>(await _api.SendAsync(HttpMethod.Get, "/api/v1/home/trending?limit=5", user));
        var badLimit = await _api.SendAsync(HttpMethod.Get, "/api/v1/home/trending?limit=99", user);

        Assert.NotEmpty(suggested.Items);
        Assert.InRange(trending.Items.Count, 1, 5);
        Assert.Equal(HttpStatusCode.BadRequest, badLimit.StatusCode);
    }

    [Fact]
    public async Task Taste_WithFewReviews_HasNoLevelsOrMatch()
    {
        var (_, _, author) = await _api.CreateVerifiedReviewAsync();
        var authorId = (await ReadAsync<UserProfileDto>(await _api.SendAsync(HttpMethod.Get, "/api/v1/users/me", author))).Id;
        var viewer = await _api.RegisterAsync();

        var taste = await ReadAsync<TasteDto>(await _api.SendAsync(HttpMethod.Get, $"/api/v1/users/{authorId}/taste", viewer));
        var match = await ReadAsync<TasteMatchDto>(await _api.SendAsync(HttpMethod.Get, $"/api/v1/users/{authorId}/taste-match", viewer));

        Assert.Equal(1, taste.ReviewCount);
        Assert.False(taste.HasEnoughReviews);
        Assert.False(match.Available);
    }

    [Fact]
    public async Task Taste_OfAnAdmin_IsNotFound()
    {
        var admin = await _api.AdminTokenAsync();
        var adminId = (await ReadAsync<UserProfileDto>(await _api.SendAsync(HttpMethod.Get, "/api/v1/users/me", admin))).Id;
        var user = await _api.RegisterAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await _api.SendAsync(HttpMethod.Get, $"/api/v1/users/{adminId}/taste", user)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _api.SendAsync(HttpMethod.Get, $"/api/v1/users/{adminId}/taste-match", user)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Get, $"/api/v1/users/{adminId}/taste", admin)).StatusCode);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ApiResponse<T>>(JsonOptions))!.Data!;
}
