using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Insaaf.Application.Auth.Dtos;
using Insaaf.Application.Common.Models;
using Insaaf.Application.Reviews.Dtos;
using Insaaf.Application.Social.Dtos;
using Insaaf.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Insaaf.API.Tests;

public sealed class UsersEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly SocialApi _api;

    public UsersEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _api = new SocialApi(factory, _client);
    }

    [Fact]
    public async Task AllRoutes_RequireAuthentication()
    {
        var id = Guid.NewGuid();

        foreach (var url in new[]
        {
            "/api/v1/users/me",
            "/api/v1/users/search?q=ab",
            $"/api/v1/users/{id}",
            $"/api/v1/users/{id}/reviews",
            $"/api/v1/users/{id}/followers",
            $"/api/v1/users/{id}/following",
        })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync(url)).StatusCode);
        }

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await _client.PutAsJsonAsync("/api/v1/users/me", new { fullName = "x", userName = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.PostAsync("/api/v1/auth/logout", null)).StatusCode);
    }

    [Fact]
    public async Task Profile_ShowsStatsFromRealReviews_AndNoPrivateData()
    {
        var (_, reviewId, author) = await _api.CreateVerifiedReviewAsync();
        var authorProfile = await ReadAsync<UserProfileDto>(await _api.SendAsync(HttpMethod.Get, "/api/v1/users/me", author));
        var viewer = await _api.RegisterAsync();
        await _api.SendAsync(HttpMethod.Put, $"/api/v1/users/{authorProfile.Id}/follow", viewer);

        var response = await _api.SendAsync(HttpMethod.Get, $"/api/v1/users/{authorProfile.Id}", viewer);
        var raw = await response.Content.ReadAsStringAsync();
        var profile = JsonSerializer.Deserialize<ApiResponse<UserProfileDto>>(raw, JsonOptions)!.Data!;

        Assert.Equal(1, profile.ReviewCount);
        Assert.Equal(1, profile.FollowerCount);
        Assert.True(profile.IsFollowedByMe);
        Assert.False(profile.IsMe);
        Assert.DoesNotContain("email", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("example.com", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("roles", raw, StringComparison.OrdinalIgnoreCase);

        var reviews = await ReadAsync<FeedResultDto>(
            await _api.SendAsync(HttpMethod.Get, $"/api/v1/users/{authorProfile.Id}/reviews", viewer));
        Assert.Equal(reviewId, Assert.Single(reviews.Items).Id);
    }

    [Fact]
    public async Task Update_ChangesOnlyTheCaller()
    {
        var token = await _api.RegisterAsync();
        var other = await _api.RegisterAsync();
        var otherBefore = await GetMeAsync(other);
        var userName = UniqueName();

        var response = await _api.SendAsync(
            HttpMethod.Put,
            "/api/v1/users/me",
            token,
            new { fullName = "  اسم جديد  ", userName, bio = "نبذة قصيرة" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await ReadAsync<UserProfileDto>(response);
        Assert.Equal("اسم جديد", updated.FullName);
        Assert.Equal(userName, updated.UserName);
        Assert.Equal("نبذة قصيرة", updated.Bio);
        Assert.Equal(otherBefore, await GetMeAsync(other));
    }

    [Fact]
    public async Task Update_RejectsExtraFieldsAndBadValues()
    {
        var token = await _api.RegisterAsync();

        var extra = await _api.SendAsync(
            HttpMethod.Put,
            "/api/v1/users/me",
            token,
            new { fullName = "x", userName = UniqueName(), email = "new@example.com" });
        var spaces = await _api.SendAsync(
            HttpMethod.Put,
            "/api/v1/users/me",
            token,
            new { fullName = "x", userName = "has spaces" });
        var longBio = await _api.SendAsync(
            HttpMethod.Put,
            "/api/v1/users/me",
            token,
            new { fullName = "x", userName = UniqueName(), bio = new string('ن', 151) });

        Assert.Equal(HttpStatusCode.BadRequest, extra.StatusCode);
        Assert.Equal("validation_error", await ErrorCodeAsync(spaces));
        Assert.Equal("validation_error", await ErrorCodeAsync(longBio));
    }

    [Fact]
    public async Task Username_IsUniqueCaseInsensitively_ThroughIdentity()
    {
        var first = await _api.RegisterAsync();
        var second = await _api.RegisterAsync();
        var name = $"Ali_{Guid.NewGuid():N}";

        var taken = await _api.SendAsync(HttpMethod.Put, "/api/v1/users/me", first, new { fullName = "x", userName = name });
        taken.EnsureSuccessStatusCode();

        var conflict = await _api.SendAsync(
            HttpMethod.Put,
            "/api/v1/users/me",
            second,
            new { fullName = "x", userName = name.ToLowerInvariant() });

        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal("username_taken", await ErrorCodeAsync(conflict));

        // Changing only your own letter case is allowed, and Identity keeps the normalized name.
        var ownCase = await _api.SendAsync(
            HttpMethod.Put,
            "/api/v1/users/me",
            first,
            new { fullName = "x", userName = name.ToLowerInvariant() });
        Assert.Equal(HttpStatusCode.OK, ownCase.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var stored = await users.FindByNameAsync(name.ToUpperInvariant());
        Assert.NotNull(stored);
        Assert.Equal(name.ToLowerInvariant(), stored.UserName);
        Assert.Equal(name.ToUpperInvariant(), stored.NormalizedUserName);
    }

    [Fact]
    public async Task SignIn_StaysByEmail_AfterAUsernameChange()
    {
        var auth = await _api.RegisterUserAsync();

        await _api.SendAsync(HttpMethod.Put, "/api/v1/users/me", auth.AccessToken, new { fullName = "x", userName = UniqueName() });

        var login = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(auth.User.Email, "Password1!"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Admin_IsNotPublic_ButKeepsTheirOwnProfile()
    {
        var admin = await _api.AdminTokenAsync();
        var adminProfile = await ReadAsync<UserProfileDto>(await _api.SendAsync(HttpMethod.Get, "/api/v1/users/me", admin));
        var user = await _api.RegisterAsync();

        foreach (var url in new[]
        {
            $"/api/v1/users/{adminProfile.Id}",
            $"/api/v1/users/{adminProfile.Id}/reviews",
            $"/api/v1/users/{adminProfile.Id}/followers",
            $"/api/v1/users/{adminProfile.Id}/following",
        })
        {
            var response = await _api.SendAsync(HttpMethod.Get, url, user);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("not_found", await ErrorCodeAsync(response));
        }

        var follow = await _api.SendAsync(HttpMethod.Put, $"/api/v1/users/{adminProfile.Id}/follow", user);
        Assert.Equal(HttpStatusCode.NotFound, follow.StatusCode);

        var search = await ReadAsync<UserSearchResultDto>(
            await _api.SendAsync(HttpMethod.Get, $"/api/v1/users/search?q={adminProfile.UserName}", user));
        Assert.Empty(search.Items);
    }

    [Fact]
    public async Task AdminComment_KeepsTheName_ButHasNoProfile()
    {
        var (_, reviewId, author) = await _api.CreateVerifiedReviewAsync();
        var admin = await _api.AdminTokenAsync();
        await _api.SendAsync(HttpMethod.Post, $"/api/v1/reviews/{reviewId}/comments", admin, new { text = "تعليق" });

        var comments = await ReadAsync<ReviewCommentListResultDto>(
            await _api.SendAsync(HttpMethod.Get, $"/api/v1/reviews/{reviewId}/comments", author));
        var detail = await ReadAsync<ReviewDetailDto>(await _api.SendAsync(HttpMethod.Get, $"/api/v1/reviews/{reviewId}", author));

        var comment = Assert.Single(comments.Items);
        Assert.NotNull(comment.Author.FullName);
        Assert.False(comment.Author.HasPublicProfile);
        Assert.True(detail.Reviewer.HasPublicProfile);
    }

    [Fact]
    public async Task Lists_ShowFollows_AndCountsAgree()
    {
        var target = await _api.RegisterAsync();
        var targetId = (await GetMeAsync(target)).Id;
        var followers = new List<string> { await _api.RegisterAsync(), await _api.RegisterAsync(), await _api.RegisterAsync() };
        foreach (var follower in followers)
        {
            await _api.SendAsync(HttpMethod.Put, $"/api/v1/users/{targetId}/follow", follower);
        }

        var seen = new List<Guid>();
        for (var page = 1; page <= 3; page++)
        {
            var result = await ReadAsync<UserListResultDto>(
                await _api.SendAsync(HttpMethod.Get, $"/api/v1/users/{targetId}/followers?page={page}&pageSize=1", target));
            seen.AddRange(result.Items.Select(item => item.Id));
            Assert.Equal(3, result.TotalCount);
        }

        Assert.Equal(3, seen.Distinct().Count());
        Assert.Equal(3, (await GetMeAsync(target)).FollowerCount);

        var following = await ReadAsync<UserListResultDto>(
            await _api.SendAsync(HttpMethod.Get, $"/api/v1/users/{seen[0]}/following", target));
        Assert.Equal(targetId, Assert.Single(following.Items).Id);
        Assert.True(Assert.Single(following.Items).IsMe);
    }

    [Fact]
    public async Task Search_FindsUsers_AndValidatesTheTerm()
    {
        var token = await _api.RegisterAsync();
        var userName = UniqueName();
        await _api.SendAsync(HttpMethod.Put, "/api/v1/users/me", token, new { fullName = "منى", userName });

        var found = await ReadAsync<UserSearchResultDto>(
            await _api.SendAsync(HttpMethod.Get, $"/api/v1/users/search?q={userName}", token));
        var shortTerm = await _api.SendAsync(HttpMethod.Get, "/api/v1/users/search?q=a", token);

        Assert.True(Assert.Single(found.Items).IsMe);
        Assert.Equal(HttpStatusCode.BadRequest, shortTerm.StatusCode);
    }

    [Fact]
    public async Task Logout_RevokesTheRefreshToken_AndIsRepeatSafe()
    {
        var auth = await _api.RegisterUserAsync();

        var first = await _api.SendAsync(HttpMethod.Post, "/api/v1/auth/logout", auth.AccessToken);
        var second = await _api.SendAsync(HttpMethod.Post, "/api/v1/auth/logout", auth.AccessToken);
        var refresh = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(auth.RefreshToken));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, refresh.StatusCode);
    }

    private static string UniqueName() => $"user_{Guid.NewGuid():N}";

    private async Task<UserProfileDto> GetMeAsync(string token) =>
        await ReadAsync<UserProfileDto>(await _api.SendAsync(HttpMethod.Get, "/api/v1/users/me", token));

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ApiResponse<T>>(JsonOptions))!.Data!;

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<object>>(JsonOptions);
        return envelope?.Errors?.FirstOrDefault()?.Code;
    }
}

public sealed class UserSearchRateLimitTests : IClassFixture<LowSocialRateLimitWebApplicationFactory>
{
    private readonly SocialApi _api;

    public UserSearchRateLimitTests(LowSocialRateLimitWebApplicationFactory factory)
    {
        _api = new SocialApi(factory, factory.CreateClient());
    }

    [Fact]
    public async Task Search_IsRateLimitedPerUser()
    {
        var user = await _api.RegisterAsync();

        var first = await _api.SendAsync(HttpMethod.Get, "/api/v1/users/search?q=zz", user);
        var second = await _api.SendAsync(HttpMethod.Get, "/api/v1/users/search?q=zz", user);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
    }
}
