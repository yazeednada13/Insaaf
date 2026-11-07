using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Insaaf.Application.Auth.Dtos;
using Insaaf.Application.Common.Models;
using Insaaf.Application.Restaurants.Dtos;
using Insaaf.Application.Reviews.Dtos;
using Insaaf.Application.Social.Dtos;
using Insaaf.Application.Verification.Dtos;
using Insaaf.Domain.Constants;
using Insaaf.Domain.Entities;
using Insaaf.Domain.Enums;
using Insaaf.Infrastructure.Identity;
using Insaaf.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Insaaf.API.Tests;

/// <summary>Social endpoints on reviews created through the real QR flow.</summary>
public sealed class SocialEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly CustomWebApplicationFactory _factory;
    private readonly SocialApi _api;

    public SocialEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _api = new SocialApi(factory, factory.CreateClient());
    }

    [Fact]
    public async Task AllRoutes_RequireAuthentication()
    {
        var reviewId = Guid.NewGuid();
        var client = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"/api/v1/reviews/{reviewId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsync($"/api/v1/reviews/{reviewId}/like", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.DeleteAsync($"/api/v1/reviews/{reviewId}/like")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"/api/v1/reviews/{reviewId}/comments")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync($"/api/v1/reviews/{reviewId}/comments", new { text = "تعليق" })).StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.DeleteAsync($"/api/v1/reviews/{reviewId}/comments/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Like_IsRepeatSafe_AndStatusIsPerUser()
    {
        var (restaurantId, reviewId, author) = await _api.CreateVerifiedReviewAsync();
        var other = await _api.RegisterAsync();

        var first = await _api.SendAsync(HttpMethod.Put, $"/api/v1/reviews/{reviewId}/like", author);
        var second = await _api.SendAsync(HttpMethod.Put, $"/api/v1/reviews/{reviewId}/like", author);
        var byOther = await _api.SendAsync(HttpMethod.Put, $"/api/v1/reviews/{reviewId}/like", other);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(1, (await ReadAsync<ReviewLikeStateDto>(second)).LikeCount);
        Assert.Equal(2, (await ReadAsync<ReviewLikeStateDto>(byOther)).LikeCount);

        var unlike = await _api.SendAsync(HttpMethod.Delete, $"/api/v1/reviews/{reviewId}/like", other);
        var state = await ReadAsync<ReviewLikeStateDto>(unlike);
        Assert.Equal(1, state.LikeCount);
        Assert.False(state.IsLikedByMe);

        var authorList = await ReadAsync<ReviewListResultDto>(
            await _api.SendAsync(HttpMethod.Get, $"/api/v1/restaurants/{restaurantId}/reviews", author));
        var otherList = await ReadAsync<ReviewListResultDto>(
            await _api.SendAsync(HttpMethod.Get, $"/api/v1/restaurants/{restaurantId}/reviews", other));

        Assert.True(authorList.Items.Single().IsLikedByMe);
        Assert.False(otherList.Items.Single().IsLikedByMe);
        Assert.Equal(1, otherList.Items.Single().LikeCount);
    }

    [Fact]
    public async Task Comments_CreateListDelete_WithOwnership()
    {
        var (_, reviewId, author) = await _api.CreateVerifiedReviewAsync();
        var other = await _api.RegisterAsync();

        var created = await _api.SendAsync(HttpMethod.Post, $"/api/v1/reviews/{reviewId}/comments", author, new { text = "  تعليق  " });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var comment = await ReadAsync<ReviewCommentDto>(created);
        Assert.Equal("تعليق", comment.Text);

        var asOther = await ReadAsync<ReviewCommentListResultDto>(
            await _api.SendAsync(HttpMethod.Get, $"/api/v1/reviews/{reviewId}/comments", other));
        Assert.False(Assert.Single(asOther.Items).IsMine);

        var forbidden = await _api.SendAsync(HttpMethod.Delete, $"/api/v1/reviews/{reviewId}/comments/{comment.Id}", other);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal("comment_forbidden", await ErrorCodeAsync(forbidden));

        var deleted = await _api.SendAsync(HttpMethod.Delete, $"/api/v1/reviews/{reviewId}/comments/{comment.Id}", author);
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.Equal(0, (await ReadAsync<ReviewCommentDeletedDto>(deleted)).CommentCount);

        var again = await _api.SendAsync(HttpMethod.Delete, $"/api/v1/reviews/{reviewId}/comments/{comment.Id}", author);
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
    }

    [Fact]
    public async Task Admin_HasNoCommentModerationInThisPhase()
    {
        var (_, reviewId, author) = await _api.CreateVerifiedReviewAsync();
        var admin = await _api.AdminTokenAsync();
        var comment = await ReadAsync<ReviewCommentDto>(
            await _api.SendAsync(HttpMethod.Post, $"/api/v1/reviews/{reviewId}/comments", author, new { text = "تعليق" }));

        var response = await _api.SendAsync(HttpMethod.Delete, $"/api/v1/reviews/{reviewId}/comments/{comment.Id}", admin);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Comment_RejectsInvalidBodies()
    {
        var (_, reviewId, author) = await _api.CreateVerifiedReviewAsync();

        var empty = await _api.SendAsync(HttpMethod.Post, $"/api/v1/reviews/{reviewId}/comments", author, new { text = "   " });
        var tooLong = await _api.SendAsync(HttpMethod.Post, $"/api/v1/reviews/{reviewId}/comments", author, new { text = new string('ن', 501) });
        var extraField = await _api.SendAsync(
            HttpMethod.Post,
            $"/api/v1/reviews/{reviewId}/comments",
            author,
            new { text = "تعليق", userId = Guid.NewGuid() });

        Assert.Equal("validation_error", await ErrorCodeAsync(empty));
        Assert.Equal("validation_error", await ErrorCodeAsync(tooLong));
        Assert.Equal(HttpStatusCode.BadRequest, extraField.StatusCode);
        Assert.Equal("validation_error", await ErrorCodeAsync(extraField));
    }

    [Fact]
    public async Task UnknownReview_IsNotFound_EverywhereWithTheEnvelope()
    {
        var user = await _api.RegisterAsync();
        var unknown = Guid.NewGuid();

        var detail = await _api.SendAsync(HttpMethod.Get, $"/api/v1/reviews/{unknown}", user);
        var like = await _api.SendAsync(HttpMethod.Put, $"/api/v1/reviews/{unknown}/like", user);
        var comments = await _api.SendAsync(HttpMethod.Get, $"/api/v1/reviews/{unknown}/comments", user);

        Assert.Equal("not_found", await ErrorCodeAsync(detail));
        Assert.Equal("not_found", await ErrorCodeAsync(like));
        Assert.Equal("not_found", await ErrorCodeAsync(comments));
        Assert.Equal(HttpStatusCode.NotFound, like.StatusCode);
    }

    [Fact]
    public async Task ReviewDetail_ReturnsSubRatingsAndCounts()
    {
        var (restaurantId, reviewId, author) = await _api.CreateVerifiedReviewAsync();
        await _api.SendAsync(HttpMethod.Put, $"/api/v1/reviews/{reviewId}/like", author);
        await _api.SendAsync(HttpMethod.Post, $"/api/v1/reviews/{reviewId}/comments", author, new { text = "تعليق" });

        var detail = await ReadAsync<ReviewDetailDto>(await _api.SendAsync(HttpMethod.Get, $"/api/v1/reviews/{reviewId}", author));

        Assert.Equal(reviewId, detail.Id);
        Assert.Equal(restaurantId, detail.Restaurant.Id);
        Assert.Equal(3, detail.ServiceRating);
        Assert.True(detail.IsVerified);
        Assert.Equal(1, detail.LikeCount);
        Assert.Equal(1, detail.CommentCount);
        Assert.True(detail.IsLikedByMe);
    }

    [Fact]
    public async Task NoNewReviewCreationPathExists()
    {
        var (restaurantId, reviewId, author) = await _api.CreateVerifiedReviewAsync();

        var standalone = await _api.SendAsync(HttpMethod.Post, "/api/v1/reviews", author, new { rating = 5 });
        var underReview = await _api.SendAsync(HttpMethod.Post, $"/api/v1/reviews/{reviewId}", author, new { rating = 5 });
        var underRestaurant = await _api.SendAsync(HttpMethod.Post, $"/api/v1/restaurants/{restaurantId}/reviews", author, new { rating = 5 });

        Assert.Equal(HttpStatusCode.NotFound, standalone.StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, underReview.StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, underRestaurant.StatusCode);
    }

    [Fact]
    public async Task LikesAndComments_DoNotChangeRestaurantRatings()
    {
        var (restaurantId, reviewId, author) = await _api.CreateVerifiedReviewAsync();
        var other = await _api.RegisterAsync();

        var before = await ReadAsync<RestaurantDetailDto>(
            await _api.SendAsync(HttpMethod.Get, $"/api/v1/restaurants/{restaurantId}", author));

        await _api.SendAsync(HttpMethod.Put, $"/api/v1/reviews/{reviewId}/like", author);
        await _api.SendAsync(HttpMethod.Put, $"/api/v1/reviews/{reviewId}/like", other);
        await _api.SendAsync(HttpMethod.Post, $"/api/v1/reviews/{reviewId}/comments", other, new { text = "تعليق" });

        var after = await ReadAsync<RestaurantDetailDto>(
            await _api.SendAsync(HttpMethod.Get, $"/api/v1/restaurants/{restaurantId}", author));

        Assert.Equal(before.AverageRating, after.AverageRating);
        Assert.Equal(before.ReviewCount, after.ReviewCount);
        Assert.Equal(before.RatingBreakdown, after.RatingBreakdown);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ApiResponse<T>>(JsonOptions))!.Data!;

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<object>>(JsonOptions);
        return envelope?.Errors?.FirstOrDefault()?.Code;
    }
}

/// <summary>Registers users and creates verified reviews through the real API.</summary>
internal sealed class SocialApi
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public SocialApi(CustomWebApplicationFactory factory, HttpClient client)
    {
        _factory = factory;
        _client = client;
    }

    public async Task<(Guid RestaurantId, Guid ReviewId, string AuthorToken)> CreateVerifiedReviewAsync()
    {
        var restaurantId = await CreatePartnerRestaurantAsync();
        var admin = await AdminTokenAsync();
        var issued = await SendAsync(
            HttpMethod.Post,
            $"/api/v1/admin/restaurants/{restaurantId}/visit-tokens",
            admin,
            new { count = 1 });
        var payload = (await issued.Content.ReadFromJsonAsync<ApiResponse<IssuedVisitTokensDto>>(JsonOptions))!
            .Data!.Payloads.Single();

        var author = await RegisterAsync();
        var created = await SendAsync(
            HttpMethod.Post,
            "/api/v1/verification/reviews",
            author,
            new { token = payload, rating = 5, foodRating = 4, serviceRating = 3, valueRating = 4, atmosphereRating = 5, text = "تقييم" });
        created.EnsureSuccessStatusCode();

        var review = (await created.Content.ReadFromJsonAsync<ApiResponse<VerifiedReviewCreatedDto>>(JsonOptions))!.Data!;
        return (restaurantId, review.ReviewId, author);
    }

    public async Task<string> RegisterAsync() => (await RegisterUserAsync()).AccessToken;

    public async Task<string> AdminTokenAsync()
    {
        var registered = await RegisterUserAsync();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await users.FindByIdAsync(registered.User.Id.ToString());
            await users.AddToRoleAsync(user!, Roles.Admin);
        }

        // Roles are embedded at sign-in, so log in again after granting Admin.
        var login = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(registered.User.Email, "Password1!"));
        login.EnsureSuccessStatusCode();
        return (await login.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>(JsonOptions))!.Data!.AccessToken;
    }

    public Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string token, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
        {
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        }

        return _client.SendAsync(request);
    }

    public async Task<AuthResponseDto> RegisterUserAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterRequest($"social-{Guid.NewGuid():N}@example.com", "Password1!", "Test User", $"user_{Guid.NewGuid():N}"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>(JsonOptions))!.Data!;
    }

    private async Task<Guid> CreatePartnerRestaurantAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var restaurant = new Restaurant
        {
            Id = Guid.NewGuid(),
            Name = $"social-{Guid.NewGuid():N}",
            Description = "وصف",
            City = "عمّان",
            Latitude = 31.95,
            Longitude = 35.92,
            PriceLevel = PriceLevel.Moderate,
            IsActive = true,
            IsPartner = true,
            CreatedAt = DateTime.UtcNow,
        };
        context.Restaurants.Add(restaurant);
        await context.SaveChangesAsync();
        return restaurant.Id;
    }
}

/// <summary>Rate limiting needs its own host with low social limits.</summary>
public sealed class LowSocialRateLimitWebApplicationFactory : CustomWebApplicationFactory
{
    protected override void ConfigureTestSettings(IWebHostBuilder builder)
    {
        builder.UseSetting("RateLimiting:SocialLikePerMinute", "2");
        builder.UseSetting("RateLimiting:SocialCommentPerMinute", "1");
        builder.UseSetting("RateLimiting:SocialFollowPerMinute", "2");
        builder.UseSetting("RateLimiting:SocialSearchPerMinute", "1");
    }
}

public sealed class SocialRateLimitTests : IClassFixture<LowSocialRateLimitWebApplicationFactory>
{
    private readonly SocialApi _api;

    public SocialRateLimitTests(LowSocialRateLimitWebApplicationFactory factory)
    {
        _api = new SocialApi(factory, factory.CreateClient());
    }

    [Fact]
    public async Task LikesAndComments_AreRateLimitedPerUser_WithEnvelope()
    {
        var user = await _api.RegisterAsync();
        var unknown = Guid.NewGuid();

        var likes = new List<HttpStatusCode>();
        string? lastBody = null;
        for (var i = 0; i < 3; i++)
        {
            var response = await _api.SendAsync(HttpMethod.Put, $"/api/v1/reviews/{unknown}/like", user);
            likes.Add(response.StatusCode);
            lastBody = await response.Content.ReadAsStringAsync();
        }

        var firstComment = await _api.SendAsync(HttpMethod.Post, $"/api/v1/reviews/{unknown}/comments", user, new { text = "١" });
        var secondComment = await _api.SendAsync(HttpMethod.Post, $"/api/v1/reviews/{unknown}/comments", user, new { text = "٢" });

        Assert.Equal([HttpStatusCode.NotFound, HttpStatusCode.NotFound, HttpStatusCode.TooManyRequests], likes);
        Assert.Contains("rate_limited", lastBody);
        Assert.Equal(HttpStatusCode.NotFound, firstComment.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, secondComment.StatusCode);
    }
}
