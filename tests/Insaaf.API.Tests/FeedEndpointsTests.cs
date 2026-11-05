using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Insaaf.Application.Common.Models;
using Insaaf.Application.Reviews.Dtos;
using Insaaf.Application.Social.Dtos;

namespace Insaaf.API.Tests;

/// <summary>Following and the feed on reviews created through the real QR flow.</summary>
public sealed class FeedEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly CustomWebApplicationFactory _factory;
    private readonly SocialApi _api;

    public FeedEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _api = new SocialApi(factory, factory.CreateClient());
    }

    [Fact]
    public async Task AllRoutes_RequireAuthentication()
    {
        var client = _factory.CreateClient();
        var userId = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/feed")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsync($"/api/v1/users/{userId}/follow", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.DeleteAsync($"/api/v1/users/{userId}/follow")).StatusCode);
    }

    [Fact]
    public async Task FullFlow_FollowSeesReview_UnfollowRemovesIt()
    {
        var (restaurantId, reviewId, author) = await _api.CreateVerifiedReviewAsync();
        var reviewerId = (await GetReviewAsync(reviewId, author)).Reviewer.Id;
        var viewer = await _api.RegisterAsync();

        Assert.Empty((await GetFeedAsync(viewer)).Items);
        Assert.False((await GetReviewAsync(reviewId, viewer)).IsReviewerFollowedByMe);

        var follow = await _api.SendAsync(HttpMethod.Put, $"/api/v1/users/{reviewerId}/follow", viewer);
        Assert.Equal(HttpStatusCode.OK, follow.StatusCode);
        Assert.True((await ReadAsync<UserFollowStateDto>(follow)).IsFollowedByMe);
        Assert.True((await GetReviewAsync(reviewId, viewer)).IsReviewerFollowedByMe);

        await _api.SendAsync(HttpMethod.Put, $"/api/v1/reviews/{reviewId}/like", viewer);
        await _api.SendAsync(HttpMethod.Post, $"/api/v1/reviews/{reviewId}/comments", viewer, new { text = "تعليق" });

        var item = Assert.Single((await GetFeedAsync(viewer)).Items);
        Assert.Equal(reviewId, item.Id);
        Assert.True(item.IsVerified);
        Assert.Equal(restaurantId, item.Restaurant.Id);
        Assert.Equal(reviewerId, item.Reviewer.Id);
        Assert.Equal(1, item.LikeCount);
        Assert.Equal(1, item.CommentCount);
        Assert.True(item.IsLikedByMe);

        var unfollow = await _api.SendAsync(HttpMethod.Delete, $"/api/v1/users/{reviewerId}/follow", viewer);
        Assert.False((await ReadAsync<UserFollowStateDto>(unfollow)).IsFollowedByMe);
        Assert.Empty((await GetFeedAsync(viewer)).Items);
    }

    [Fact]
    public async Task Feed_ExcludesTheAuthorsOwnReview()
    {
        var (_, _, author) = await _api.CreateVerifiedReviewAsync();

        Assert.Empty((await GetFeedAsync(author)).Items);
    }

    [Fact]
    public async Task Follow_RejectsSelfAndUnknownUsers()
    {
        var (_, reviewId, author) = await _api.CreateVerifiedReviewAsync();
        var authorId = (await GetReviewAsync(reviewId, author)).Reviewer.Id;

        var self = await _api.SendAsync(HttpMethod.Put, $"/api/v1/users/{authorId}/follow", author);
        var unknown = await _api.SendAsync(HttpMethod.Put, $"/api/v1/users/{Guid.NewGuid()}/follow", author);

        Assert.Equal(HttpStatusCode.BadRequest, self.StatusCode);
        Assert.Equal("cannot_follow_self", await ErrorCodeAsync(self));
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal("not_found", await ErrorCodeAsync(unknown));
    }

    [Fact]
    public async Task Feed_RejectsBadCursorAndLimit()
    {
        var viewer = await _api.RegisterAsync();

        var badCursor = await _api.SendAsync(HttpMethod.Get, "/api/v1/feed?cursor=nope", viewer);
        var badLimit = await _api.SendAsync(HttpMethod.Get, "/api/v1/feed?limit=51", viewer);

        Assert.Equal("validation_error", await ErrorCodeAsync(badCursor));
        Assert.Equal(HttpStatusCode.BadRequest, badLimit.StatusCode);
    }

    private async Task<FeedResultDto> GetFeedAsync(string token) =>
        await ReadAsync<FeedResultDto>(await _api.SendAsync(HttpMethod.Get, "/api/v1/feed", token));

    private async Task<ReviewDetailDto> GetReviewAsync(Guid reviewId, string token) =>
        await ReadAsync<ReviewDetailDto>(await _api.SendAsync(HttpMethod.Get, $"/api/v1/reviews/{reviewId}", token));

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ApiResponse<T>>(JsonOptions))!.Data!;

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<object>>(JsonOptions);
        return envelope?.Errors?.FirstOrDefault()?.Code;
    }
}

public sealed class FollowRateLimitTests : IClassFixture<LowSocialRateLimitWebApplicationFactory>
{
    private readonly SocialApi _api;

    public FollowRateLimitTests(LowSocialRateLimitWebApplicationFactory factory)
    {
        _api = new SocialApi(factory, factory.CreateClient());
    }

    [Fact]
    public async Task Follow_IsRateLimitedPerUser()
    {
        var viewer = await _api.RegisterAsync();
        var unknown = Guid.NewGuid();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++)
        {
            statuses.Add((await _api.SendAsync(HttpMethod.Put, $"/api/v1/users/{unknown}/follow", viewer)).StatusCode);
        }

        Assert.Equal([HttpStatusCode.NotFound, HttpStatusCode.NotFound, HttpStatusCode.TooManyRequests], statuses);
    }
}
