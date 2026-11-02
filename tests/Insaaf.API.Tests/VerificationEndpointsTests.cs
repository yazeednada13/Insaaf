using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Insaaf.Application.Admin;
using Insaaf.Application.Auth.Dtos;
using Insaaf.Application.Common.Models;
using Insaaf.Application.Restaurants.Dtos;
using Insaaf.Application.Reviews.Dtos;
using Insaaf.Application.Verification.Dtos;
using Insaaf.Domain.Constants;
using Insaaf.Domain.Entities;
using Insaaf.Domain.Enums;
using Insaaf.Infrastructure.Identity;
using Insaaf.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Insaaf.API.Tests;

public sealed class VerificationEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public VerificationEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ── Admin authorization ───────────────────────────────────────────────────

    [Fact]
    public async Task AdminEndpoints_RejectAnonymousAndNonAdminUsers()
    {
        var restaurantId = await CreateRestaurantAsync(isPartner: true);
        var user = await RegisterAsync();

        var anonymous = await _client.PostAsJsonAsync($"/api/v1/admin/restaurants/{restaurantId}/visit-tokens", new { count = 1 });
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var asUser = await SendAsync(HttpMethod.Post, $"/api/v1/admin/restaurants/{restaurantId}/visit-tokens", user.AccessToken, new { count = 1 });
        Assert.Equal(HttpStatusCode.Forbidden, asUser.StatusCode);

        var partnershipAsUser = await SendAsync(HttpMethod.Put, $"/api/v1/admin/restaurants/{restaurantId}/partnership", user.AccessToken, new { isPartner = true });
        Assert.Equal(HttpStatusCode.Forbidden, partnershipAsUser.StatusCode);
    }

    [Fact]
    public async Task Admin_CannotIssueForNonPartner_UntilPartnershipIsSet()
    {
        var admin = await AdminTokenAsync();
        var restaurantId = await CreateRestaurantAsync(isPartner: false);

        var refused = await SendAsync(HttpMethod.Post, $"/api/v1/admin/restaurants/{restaurantId}/visit-tokens", admin, new { count = 1 });
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal("restaurant_not_partner", (await ErrorCodeAsync(refused)));

        var partnership = await SendAsync(HttpMethod.Put, $"/api/v1/admin/restaurants/{restaurantId}/partnership", admin, new { isPartner = true });
        Assert.Equal(HttpStatusCode.OK, partnership.StatusCode);
        var partnershipDto = (await partnership.Content.ReadFromJsonAsync<ApiResponse<RestaurantPartnershipDto>>(JsonOptions))!.Data!;
        Assert.True(partnershipDto.IsPartner);
        Assert.NotNull(partnershipDto.PartnerSince);

        var issued = await IssueAsync(admin, restaurantId, count: 3);
        Assert.Equal(3, issued.Payloads.Count);
        Assert.All(issued.Payloads, payload => Assert.StartsWith("insaaf://visit/", payload));
        Assert.Equal(3, issued.Payloads.Distinct().Count());
    }

    // ── The verified flow ──────────────────────────────────────────────────────

    [Fact]
    public async Task FullFlow_ResolveThenCreate_ReviewIsPublicOnTokensRestaurant_AndTokenIsSingleUse()
    {
        var admin = await AdminTokenAsync();
        var restaurantId = await CreateRestaurantAsync(isPartner: true);
        var payload = (await IssueAsync(admin, restaurantId, count: 1)).Payloads.Single();
        var customer = await RegisterAsync();

        var resolve = await SendAsync(HttpMethod.Post, "/api/v1/verification/visits/resolve", customer.AccessToken, new { token = payload });
        Assert.Equal(HttpStatusCode.OK, resolve.StatusCode);
        var resolved = (await resolve.Content.ReadFromJsonAsync<ApiResponse<ResolvedVisitDto>>(JsonOptions))!.Data!;
        Assert.Equal(restaurantId, resolved.Restaurant.Id);

        var create = await SendAsync(HttpMethod.Post, "/api/v1/verification/reviews", customer.AccessToken, ReviewBody(payload));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = (await create.Content.ReadFromJsonAsync<ApiResponse<VerifiedReviewCreatedDto>>(JsonOptions))!.Data!;
        Assert.Equal(restaurantId, created.RestaurantId);

        // The review is public, verified, and counted in the aggregates.
        var reviews = await SendAsync(HttpMethod.Get, $"/api/v1/restaurants/{restaurantId}/reviews", customer.AccessToken);
        var list = (await reviews.Content.ReadFromJsonAsync<ApiResponse<ReviewListResultDto>>(JsonOptions))!.Data!;
        var review = Assert.Single(list.Items);
        Assert.Equal(created.ReviewId, review.Id);
        Assert.True(review.IsVerified);
        Assert.Equal(customer.User.Id, review.Reviewer.Id);

        var detail = await SendAsync(HttpMethod.Get, $"/api/v1/restaurants/{restaurantId}", customer.AccessToken);
        var detailDto = (await detail.Content.ReadFromJsonAsync<ApiResponse<RestaurantDetailDto>>(JsonOptions))!.Data!;
        Assert.Equal(1, detailDto.ReviewCount);
        Assert.Equal(5m, detailDto.AverageRating);

        // Single use: a second submit or resolve is rejected as used.
        var second = await SendAsync(HttpMethod.Post, "/api/v1/verification/reviews", customer.AccessToken, ReviewBody(payload));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("token_used", await ErrorCodeAsync(second));

        var resolveAgain = await SendAsync(HttpMethod.Post, "/api/v1/verification/visits/resolve", customer.AccessToken, new { token = payload });
        Assert.Equal(HttpStatusCode.Conflict, resolveAgain.StatusCode);
    }

    [Fact]
    public async Task Create_RejectsClientSuppliedRestaurantId()
    {
        var admin = await AdminTokenAsync();
        var restaurantId = await CreateRestaurantAsync(isPartner: true);
        var otherRestaurantId = await CreateRestaurantAsync(isPartner: true);
        var payload = (await IssueAsync(admin, restaurantId, count: 1)).Payloads.Single();
        var customer = await RegisterAsync();

        var body = new
        {
            token = payload,
            restaurantId = otherRestaurantId,
            rating = 5,
            foodRating = 5,
            serviceRating = 5,
            valueRating = 5,
            atmosphereRating = 5,
            text = "override attempt",
        };
        var response = await SendAsync(HttpMethod.Post, "/api/v1/verification/reviews", customer.AccessToken, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation_error", await ErrorCodeAsync(response));
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await context.Reviews.AnyAsync(review => review.RestaurantId == otherRestaurantId));
        Assert.Null((await context.VisitTokens.AsNoTracking().SingleAsync(token => token.RestaurantId == restaurantId)).RedeemedAt);
    }

    [Fact]
    public async Task NoOtherReviewCreationPathExists()
    {
        var customer = await RegisterAsync();
        var restaurantId = await CreateRestaurantAsync(isPartner: true);

        var standalone = await SendAsync(HttpMethod.Post, "/api/v1/reviews", customer.AccessToken, ReviewBody("x"));
        Assert.Equal(HttpStatusCode.NotFound, standalone.StatusCode);

        var underRestaurant = await SendAsync(HttpMethod.Post, $"/api/v1/restaurants/{restaurantId}/reviews", customer.AccessToken, ReviewBody("x"));
        Assert.Equal(HttpStatusCode.MethodNotAllowed, underRestaurant.StatusCode);
    }

    [Fact]
    public async Task Tokens_ExpiredRevokedUnknownAndAnonymous_AreRejected()
    {
        var admin = await AdminTokenAsync();
        var restaurantId = await CreateRestaurantAsync(isPartner: true);
        var issued = await IssueAsync(admin, restaurantId, count: 2);
        var customer = await RegisterAsync();

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var token = await context.VisitTokens.FirstAsync(item => item.IssuanceId == issued.IssuanceId);
            token.ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
            token.IssuedAt = DateTime.UtcNow.AddDays(-1);
            await context.SaveChangesAsync();
        }

        var results = new List<(HttpStatusCode Status, string? Code)>();
        foreach (var payload in issued.Payloads)
        {
            var response = await SendAsync(HttpMethod.Post, "/api/v1/verification/visits/resolve", customer.AccessToken, new { token = payload });
            results.Add((response.StatusCode, await ErrorCodeAsync(response)));
        }

        Assert.Contains((HttpStatusCode.Gone, "token_expired"), results);
        Assert.Contains(results, result => result.Status == HttpStatusCode.OK);

        var revoke = await SendAsync(HttpMethod.Post, $"/api/v1/admin/restaurants/{restaurantId}/visit-tokens/revoke", admin, new { issuanceId = issued.IssuanceId });
        var revoked = (await revoke.Content.ReadFromJsonAsync<ApiResponse<RevokedVisitTokensDto>>(JsonOptions))!.Data!;
        Assert.Equal(2, revoked.RevokedCount);

        foreach (var payload in issued.Payloads)
        {
            var response = await SendAsync(HttpMethod.Post, "/api/v1/verification/visits/resolve", customer.AccessToken, new { token = payload });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("token_invalid", await ErrorCodeAsync(response));
        }

        var unknown = await SendAsync(HttpMethod.Post, "/api/v1/verification/visits/resolve", customer.AccessToken, new { token = "insaaf://visit/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" });
        Assert.Equal("token_invalid", await ErrorCodeAsync(unknown));

        var anonymous = await _client.PostAsJsonAsync("/api/v1/verification/visits/resolve", new { token = issued.Payloads[0] });
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var summary = await SendAsync(HttpMethod.Get, $"/api/v1/admin/restaurants/{restaurantId}/visit-tokens/summary", admin);
        var counts = (await summary.Content.ReadFromJsonAsync<ApiResponse<VisitTokenSummaryDto>>(JsonOptions))!.Data!;
        Assert.Equal(new VisitTokenSummaryDto(Active: 0, Redeemed: 0, Expired: 0, Revoked: 2), counts);
    }

    [Fact]
    public async Task PartnershipEnded_InvalidatesExistingTokens()
    {
        var admin = await AdminTokenAsync();
        var restaurantId = await CreateRestaurantAsync(isPartner: true);
        var payload = (await IssueAsync(admin, restaurantId, count: 1)).Payloads.Single();
        var customer = await RegisterAsync();

        await SendAsync(HttpMethod.Put, $"/api/v1/admin/restaurants/{restaurantId}/partnership", admin, new { isPartner = false });

        var create = await SendAsync(HttpMethod.Post, "/api/v1/verification/reviews", customer.AccessToken, ReviewBody(payload));
        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);
        Assert.Equal("token_invalid", await ErrorCodeAsync(create));
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static object ReviewBody(string token) =>
        new { token, rating = 5, foodRating = 5, serviceRating = 4, valueRating = 4, atmosphereRating = 5, text = "تجربة رائعة" };

    private async Task<IssuedVisitTokensDto> IssueAsync(string adminToken, Guid restaurantId, int count)
    {
        var response = await SendAsync(HttpMethod.Post, $"/api/v1/admin/restaurants/{restaurantId}/visit-tokens", adminToken, new { count });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ApiResponse<IssuedVisitTokensDto>>(JsonOptions))!.Data!;
    }

    private async Task<string> AdminTokenAsync()
    {
        var registered = await RegisterAsync();
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

    private async Task<AuthResponseDto> RegisterAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterRequest($"verify-{Guid.NewGuid():N}@example.com", "Password1!", "Test User", $"user_{Guid.NewGuid():N}"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>(JsonOptions))!.Data!;
    }

    private async Task<Guid> CreateRestaurantAsync(bool isPartner)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var restaurant = new Restaurant
        {
            Id = Guid.NewGuid(),
            Name = $"verify-{Guid.NewGuid():N}",
            Description = "وصف",
            City = "عمّان",
            Latitude = 31.95,
            Longitude = 35.92,
            PriceLevel = PriceLevel.Moderate,
            IsActive = true,
            IsPartner = isPartner,
            CreatedAt = DateTime.UtcNow,
        };
        context.Restaurants.Add(restaurant);
        await context.SaveChangesAsync();
        return restaurant.Id;
    }

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string token, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
        {
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        }

        return _client.SendAsync(request);
    }

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<object>>(JsonOptions);
        return envelope?.Errors?.FirstOrDefault()?.Code;
    }
}

/// <summary>Rate limiting needs its own host with a low create limit.</summary>
public sealed class LowRateLimitWebApplicationFactory : CustomWebApplicationFactory
{
    protected override void ConfigureTestSettings(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseSetting("RateLimiting:VerificationResolvePerMinute", "2");
    }
}

public sealed class VerificationRateLimitTests : IClassFixture<LowRateLimitWebApplicationFactory>
{
    private readonly HttpClient _client;

    public VerificationRateLimitTests(LowRateLimitWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Resolve_IsRateLimitedPerUser_WithEnvelope()
    {
        var register = await _client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterRequest($"rate-{Guid.NewGuid():N}@example.com", "Password1!", "Rate User", $"user_{Guid.NewGuid():N}"));
        var auth = (await register.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>(
            new JsonSerializerOptions(JsonSerializerDefaults.Web)))!.Data!;

        var statuses = new List<HttpStatusCode>();
        string? lastBody = null;
        for (var i = 0; i < 3; i++)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/verification/visits/resolve")
            {
                Content = JsonContent.Create(new { token = "not-a-token" }),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
            var response = await _client.SendAsync(request);
            statuses.Add(response.StatusCode);
            lastBody = await response.Content.ReadAsStringAsync();
        }

        Assert.Equal([HttpStatusCode.BadRequest, HttpStatusCode.BadRequest, HttpStatusCode.TooManyRequests], statuses);
        Assert.Contains("rate_limited", lastBody);
    }
}
