using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Insaaf.Application.Auth.Dtos;
using Insaaf.Application.Common.Models;
using Insaaf.Application.Restaurants.Dtos;
using Insaaf.Domain.Entities;
using Insaaf.Domain.Enums;
using Insaaf.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Insaaf.API.Tests;

public sealed class RestaurantEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public RestaurantEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task List_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        var response = await _client.GetAsync("/api/v1/restaurants");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task List_ReturnsSeededActiveRestaurants()
    {
        var token = await GetAccessTokenAsync();
        using var request = Authorized(HttpMethod.Get, "/api/v1/restaurants", token);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<RestaurantListResultDto>>(JsonOptions);
        Assert.NotNull(envelope);
        Assert.True(envelope.Success);
        Assert.True(envelope.Data!.TotalCount >= 11);
        Assert.All(envelope.Data.Items, item => Assert.Null(item.AverageRating));
        Assert.DoesNotContain(envelope.Data.Items, item => item.Name == "مطعم مغلق للتجارب");
    }

    [Fact]
    public async Task List_FiltersBySearchAndCategory()
    {
        var token = await GetAccessTokenAsync();
        using var request = Authorized(
            HttpMethod.Get,
            "/api/v1/restaurants?search=السفير&category=lebanese",
            token);

        var response = await _client.SendAsync(request);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<RestaurantListResultDto>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(envelope?.Data);
        Assert.Contains(envelope.Data.Items, item => item.Name.Contains("السفير"));
        Assert.All(envelope.Data.Items, item => Assert.Contains(item.Categories, category => category.Slug == "lebanese"));
    }

    [Fact]
    public async Task List_ReturnsEmpty_WhenMinRatingIsSet()
    {
        var token = await GetAccessTokenAsync();
        using var request = Authorized(HttpMethod.Get, "/api/v1/restaurants?minRating=4", token);

        var response = await _client.SendAsync(request);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<RestaurantListResultDto>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(envelope?.Data);
        Assert.Empty(envelope.Data.Items);
        Assert.Equal(0, envelope.Data.TotalCount);
    }

    [Fact]
    public async Task List_ReturnsValidationError_WhenPageSizeIsTooLarge()
    {
        var token = await GetAccessTokenAsync();
        using var request = Authorized(HttpMethod.Get, "/api/v1/restaurants?pageSize=99", token);

        var response = await _client.SendAsync(request);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<RestaurantListResultDto>>(JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(envelope!.Success);
        Assert.Contains(envelope.Errors!, error => error.Code == "validation_error");
    }

    [Fact]
    public async Task Detail_ReturnsRestaurant_WhenIdIsValid()
    {
        var token = await GetAccessTokenAsync();
        var list = await GetListAsync(token);
        var id = list.Items[0].Id;

        using var request = Authorized(HttpMethod.Get, $"/api/v1/restaurants/{id}", token);
        var response = await _client.SendAsync(request);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<RestaurantDetailDto>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(envelope!.Success);
        Assert.Equal(id, envelope.Data!.Id);
        Assert.False(string.IsNullOrWhiteSpace(envelope.Data.Description));
        Assert.Null(envelope.Data.AverageRating);
        Assert.Equal(0, envelope.Data.ReviewCount);
    }

    [Fact]
    public async Task Detail_ReturnsNotFound_WhenIdIsUnknownOrInvalid()
    {
        var token = await GetAccessTokenAsync();

        using var missing = Authorized(HttpMethod.Get, $"/api/v1/restaurants/{Guid.NewGuid()}", token);
        var missingResponse = await _client.SendAsync(missing);
        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);

        using var invalid = Authorized(HttpMethod.Get, "/api/v1/restaurants/not-a-guid", token);
        var invalidResponse = await _client.SendAsync(invalid);
        Assert.Equal(HttpStatusCode.NotFound, invalidResponse.StatusCode);
    }

    [Fact]
    public async Task Detail_ReturnsNotFound_WhenRestaurantIsInactive()
    {
        Guid inactiveId;
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var inactive = new Restaurant
            {
                Id = Guid.NewGuid(),
                Name = $"inactive-{Guid.NewGuid():N}",
                Description = "مخفي",
                City = "عمّان",
                Latitude = 31.9,
                Longitude = 35.9,
                PriceLevel = PriceLevel.Budget,
                IsActive = false,
                CreatedAt = DateTime.UtcNow,
            };
            context.Restaurants.Add(inactive);
            await context.SaveChangesAsync();
            inactiveId = inactive.Id;
        }

        var token = await GetAccessTokenAsync();
        using var request = Authorized(HttpMethod.Get, $"/api/v1/restaurants/{inactiveId}", token);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<string> GetAccessTokenAsync()
    {
        var email = $"resto-{Guid.NewGuid():N}@example.com";
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterRequest(email, "Password1!", "Test User", $"user_{Guid.NewGuid():N}"));
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>(JsonOptions);
        Assert.NotNull(envelope?.Data?.AccessToken);
        return envelope.Data.AccessToken;
    }

    private async Task<RestaurantListResultDto> GetListAsync(string token)
    {
        using var request = Authorized(HttpMethod.Get, "/api/v1/restaurants", token);
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<RestaurantListResultDto>>(JsonOptions);
        Assert.NotNull(envelope?.Data);
        return envelope.Data;
    }

    private static HttpRequestMessage Authorized(HttpMethod method, string url, string token)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}
