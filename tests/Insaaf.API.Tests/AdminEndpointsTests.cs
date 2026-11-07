using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Insaaf.Application.Admin.Dtos;
using Insaaf.Application.Auth.Dtos;
using Insaaf.Application.Common.Models;
using Insaaf.Application.Social.Dtos;
using Insaaf.Domain.Entities;
using Insaaf.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Insaaf.API.Tests;

/// <summary>Phase 9 Admin routes: role checks, moderation on QR-created reviews, suspension at sign-in.</summary>
public sealed class AdminEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly SocialApi _api;

    public AdminEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _api = new SocialApi(factory, _client);
    }

    private static IEnumerable<(HttpMethod Method, string Url, object? Body)> AdminRoutes()
    {
        var id = Guid.NewGuid();
        yield return (HttpMethod.Get, "/api/v1/admin/restaurants", null);
        yield return (HttpMethod.Post, "/api/v1/admin/restaurants", new { });
        yield return (HttpMethod.Put, $"/api/v1/admin/restaurants/{id}", new { });
        yield return (HttpMethod.Put, $"/api/v1/admin/restaurants/{id}/status", new { isActive = false });
        yield return (HttpMethod.Get, "/api/v1/admin/reviews", null);
        yield return (HttpMethod.Put, $"/api/v1/admin/reviews/{id}/visibility", new { hidden = true });
        yield return (HttpMethod.Delete, $"/api/v1/admin/comments/{id}", null);
        yield return (HttpMethod.Get, "/api/v1/admin/users?query=abc", null);
        yield return (HttpMethod.Put, $"/api/v1/admin/users/{id}/suspension", new { suspended = true });
    }

    [Fact]
    public async Task EveryAdminRoute_Is401Anonymous_And403ForAUser()
    {
        var user = await _api.RegisterAsync();

        foreach (var (method, url, body) in AdminRoutes())
        {
            var anonymous = new HttpRequestMessage(method, url);
            if (body is not null)
            {
                anonymous.Content = JsonContent.Create(body);
            }

            Assert.Equal(HttpStatusCode.Unauthorized, (await _client.SendAsync(anonymous)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await _api.SendAsync(method, url, user, body)).StatusCode);
        }
    }

    [Fact]
    public async Task Restaurant_CreateUpdateAndDeactivate()
    {
        var admin = await _api.AdminTokenAsync();
        var slug = await EnsureCategoryAsync();
        var body = RestaurantBody(slug);

        var created = await _api.SendAsync(HttpMethod.Post, "/api/v1/admin/restaurants", admin, body);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var restaurant = await ReadAsync<AdminRestaurantDto>(created);
        Assert.Equal("Asia/Amman", restaurant.TimeZoneId);
        Assert.Equal(2, restaurant.OpeningHours.Count);

        // A Windows id and a cross-day overlap are both refused.
        var windowsZone = await _api.SendAsync(HttpMethod.Put, $"/api/v1/admin/restaurants/{restaurant.Id}", admin,
            RestaurantBody(slug, timeZoneId: "Arab Standard Time"));
        var overlap = await _api.SendAsync(HttpMethod.Put, $"/api/v1/admin/restaurants/{restaurant.Id}", admin,
            RestaurantBody(slug, hours: [new { dayOfWeek = 0, opensAt = "22:00", closesAt = "02:00" }, new { dayOfWeek = 1, opensAt = "01:00", closesAt = "03:00" }]));
        Assert.Equal(HttpStatusCode.BadRequest, windowsZone.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, overlap.StatusCode);

        // An omitted openingHours property is refused, not treated as "clear".
        var omitted = await _api.SendAsync(HttpMethod.Put, $"/api/v1/admin/restaurants/{restaurant.Id}", admin, new
        {
            name = "x", city = "Amman", latitude = 31.9, longitude = 35.9, priceLevel = 2, timeZoneId = "Asia/Amman",
            categorySlugs = new[] { slug },
        });
        Assert.Equal(HttpStatusCode.BadRequest, omitted.StatusCode);

        // An unknown property such as actorId is refused: the actor comes from the JWT only.
        var withActor = await _api.SendAsync(HttpMethod.Put, $"/api/v1/admin/restaurants/{restaurant.Id}/status", admin,
            new { isActive = false, actorId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.BadRequest, withActor.StatusCode);

        var deactivated = await _api.SendAsync(HttpMethod.Put, $"/api/v1/admin/restaurants/{restaurant.Id}/status", admin, new { isActive = false });
        Assert.False((await ReadAsync<AdminRestaurantDto>(deactivated)).IsActive);
        Assert.Equal(HttpStatusCode.NotFound, (await _api.SendAsync(HttpMethod.Get, $"/api/v1/restaurants/{restaurant.Id}", admin)).StatusCode);
    }

    [Fact]
    public async Task HideAndUnhide_OnAQrCreatedReview()
    {
        var (_, reviewId, author) = await _api.CreateVerifiedReviewAsync();
        var reader = await _api.RegisterAsync();
        var admin = await _api.AdminTokenAsync();

        var hidden = await _api.SendAsync(HttpMethod.Put, $"/api/v1/admin/reviews/{reviewId}/visibility", admin,
            new { hidden = true, reason = "internal-note-xyz" });
        Assert.True((await ReadAsync<AdminReviewDto>(hidden)).IsHidden);

        Assert.Equal(HttpStatusCode.NotFound, (await _api.SendAsync(HttpMethod.Get, $"/api/v1/reviews/{reviewId}", reader)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _api.SendAsync(HttpMethod.Put, $"/api/v1/reviews/{reviewId}/like", reader)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await _api.SendAsync(HttpMethod.Post, $"/api/v1/reviews/{reviewId}/comments", reader, new { text = "تعليق" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _api.SendAsync(HttpMethod.Get, $"/api/v1/reviews/{reviewId}/comments", reader)).StatusCode);

        var shown = await _api.SendAsync(HttpMethod.Put, $"/api/v1/admin/reviews/{reviewId}/visibility", admin, new { hidden = false });
        Assert.False((await ReadAsync<AdminReviewDto>(shown)).IsHidden);

        var detail = await _api.SendAsync(HttpMethod.Get, $"/api/v1/reviews/{reviewId}", reader);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        Assert.DoesNotContain("internal-note-xyz", await detail.Content.ReadAsStringAsync());
        Assert.DoesNotContain("hidden", await detail.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Put, $"/api/v1/reviews/{reviewId}/like", reader)).StatusCode);
        Assert.Equal(HttpStatusCode.Created,
            (await _api.SendAsync(HttpMethod.Post, $"/api/v1/reviews/{reviewId}/comments", reader, new { text = "رجع" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Get, "/api/v1/users/me", author)).StatusCode);
    }

    [Fact]
    public async Task AdminDeletesAnotherUsersComment()
    {
        var (_, reviewId, _) = await _api.CreateVerifiedReviewAsync();
        var commenter = await _api.RegisterAsync();
        var admin = await _api.AdminTokenAsync();
        var comment = await ReadAsync<ReviewCommentDto>(
            await _api.SendAsync(HttpMethod.Post, $"/api/v1/reviews/{reviewId}/comments", commenter, new { text = "سيء" }));

        var deleted = await _api.SendAsync(HttpMethod.Delete, $"/api/v1/admin/comments/{comment.Id}", admin);
        var again = await _api.SendAsync(HttpMethod.Delete, $"/api/v1/admin/comments/{comment.Id}", admin);

        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
        Assert.Empty((await ReadAsync<ReviewCommentListResultDto>(
            await _api.SendAsync(HttpMethod.Get, $"/api/v1/reviews/{reviewId}/comments", commenter))).Items);
    }

    [Fact]
    public async Task SuspendedUser_CantSignInOrRefresh_AndUnsuspendRestoresSignIn()
    {
        var user = await _api.RegisterUserAsync();
        var admin = await _api.AdminTokenAsync();

        var suspended = await _api.SendAsync(HttpMethod.Put, $"/api/v1/admin/users/{user.User.Id}/suspension", admin, new { suspended = true });
        Assert.True((await ReadAsync<AdminUserDto>(suspended)).IsSuspended);

        var login = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(user.User.Email, "Password1!"));
        var wrongPassword = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(user.User.Email, "Wrong1!pass"));
        var refresh = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(user.RefreshToken));
        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
        Assert.Contains("account_suspended", await login.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);

        // Documented limitation (ADR-017): the access token issued before suspension still works until it expires.
        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Get, "/api/v1/users/me", user.AccessToken)).StatusCode);

        // An Admin account can't be suspended.
        var adminId = (await ReadAsync<UserProfileDto>(await _api.SendAsync(HttpMethod.Get, "/api/v1/users/me", admin))).Id;
        Assert.Equal(HttpStatusCode.Forbidden,
            (await _api.SendAsync(HttpMethod.Put, $"/api/v1/admin/users/{adminId}/suspension", admin, new { suspended = true })).StatusCode);

        await _api.SendAsync(HttpMethod.Put, $"/api/v1/admin/users/{user.User.Id}/suspension", admin, new { suspended = false });
        var after = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(user.User.Email, "Password1!"));
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
    }

    [Fact]
    public async Task AdminLists_ReturnHiddenReviewsAndUsers_ButPublicJsonHasNoModerationFields()
    {
        var (restaurantId, reviewId, _) = await _api.CreateVerifiedReviewAsync();
        var user = await _api.RegisterUserAsync();
        var admin = await _api.AdminTokenAsync();
        await _api.SendAsync(HttpMethod.Put, $"/api/v1/admin/reviews/{reviewId}/visibility", admin, new { hidden = true });

        var reviews = await ReadAsync<AdminReviewListResultDto>(
            await _api.SendAsync(HttpMethod.Get, $"/api/v1/admin/reviews?restaurantId={restaurantId}", admin));
        var users = await ReadAsync<AdminUserSearchResultDto>(
            await _api.SendAsync(HttpMethod.Get, $"/api/v1/admin/users?query={user.User.Email}", admin));

        Assert.True(Assert.Single(reviews.Items).IsHidden);
        Assert.Equal(user.User.Id, Assert.Single(users.Items).Id);

        var profile = await _api.SendAsync(HttpMethod.Get, $"/api/v1/users/{user.User.Id}", user.AccessToken);
        Assert.DoesNotContain("suspended", await profile.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    private static object RestaurantBody(string slug, string timeZoneId = "Asia/Amman", object[]? hours = null) => new
    {
        name = $"admin-{Guid.NewGuid():N}",
        description = "وصف",
        city = "Amman",
        address = "شارع",
        latitude = 31.95,
        longitude = 35.91,
        priceLevel = 2,
        phone = (string?)null,
        imageUrl = (string?)null,
        timeZoneId,
        categorySlugs = new[] { slug },
        openingHours = hours ??
        [
            new { dayOfWeek = 0, opensAt = "12:00", closesAt = "23:00" },
            new { dayOfWeek = 5, opensAt = "18:00", closesAt = "02:00" },
        ],
    };

    private async Task<string> EnsureCategoryAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var existing = await context.Categories.Select(category => category.Slug).FirstOrDefaultAsync();
        if (existing is not null)
        {
            return existing;
        }

        context.Categories.Add(new Category { Id = Guid.NewGuid(), Name = "برجر", Slug = "burger" });
        await context.SaveChangesAsync();
        return "burger";
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ApiResponse<T>>(JsonOptions))!.Data!;
}
