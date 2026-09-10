using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Insaaf.Application.Auth.Dtos;
using Insaaf.Application.Common.Models;
using Insaaf.Domain.Entities;
using Insaaf.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Insaaf.API.Tests;

public sealed class AuthEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public AuthEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_ReturnsTokens_WhenRequestIsValid()
    {
        var email = $"user-{Guid.NewGuid():N}@example.com";
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/register",
            CreateRegisterRequest(email));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>(JsonOptions);
        Assert.NotNull(envelope);
        Assert.True(envelope.Success);
        Assert.NotNull(envelope.Data?.AccessToken);
        Assert.NotNull(envelope.Data.RefreshToken);
        Assert.Equal(email, envelope.Data.User.Email);
    }

    [Fact]
    public async Task Register_ReturnsValidationError_WhenPasswordIsWeak()
    {
        var email = $"weak-{Guid.NewGuid():N}@example.com";
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/register",
            CreateRegisterRequest(email, password: "short"));

        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>(JsonOptions);
        Assert.NotNull(envelope);
        Assert.False(envelope.Success);
        Assert.Contains(envelope.Errors!, error => error.Code == "validation_error");
    }

    [Fact]
    public async Task Login_ReturnsTokens_WhenCredentialsAreValid()
    {
        var email = $"login-ok-{Guid.NewGuid():N}@example.com";
        await _client.PostAsJsonAsync("/api/v1/auth/register", CreateRegisterRequest(email));

        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(email, "Password1!"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>(JsonOptions);
        Assert.NotNull(envelope);
        Assert.True(envelope.Success);
        Assert.NotNull(envelope.Data?.AccessToken);
    }

    [Fact]
    public async Task Register_ReturnsDuplicateEmail_WhenEmailAlreadyExists()
    {
        var email = $"dup-{Guid.NewGuid():N}@example.com";
        await _client.PostAsJsonAsync("/api/v1/auth/register", CreateRegisterRequest(email));

        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/register",
            CreateRegisterRequest(email));

        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>(JsonOptions);
        Assert.NotNull(envelope);
        Assert.False(envelope.Success);
        Assert.Contains(envelope.Errors!, error => error.Code == "duplicate_email");
    }

    [Fact]
    public async Task Login_ReturnsInvalidCredentials_WhenPasswordIsWrong()
    {
        var email = $"login-{Guid.NewGuid():N}@example.com";
        await _client.PostAsJsonAsync("/api/v1/auth/register", CreateRegisterRequest(email));

        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(email, "WrongPassword1!"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>(JsonOptions);
        Assert.NotNull(envelope);
        Assert.False(envelope.Success);
        Assert.Contains(envelope.Errors!, error => error.Code == "invalid_credentials");
    }

    [Fact]
    public async Task Login_ReturnsInvalidCredentials_WhenAccountIsLockedOut()
    {
        var email = $"lockout-{Guid.NewGuid():N}@example.com";
        await _client.PostAsJsonAsync("/api/v1/auth/register", CreateRegisterRequest(email));

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var failedAttempt = await _client.PostAsJsonAsync(
                "/api/v1/auth/login",
                new LoginRequest(email, "WrongPassword1!"));

            Assert.Equal(HttpStatusCode.Unauthorized, failedAttempt.StatusCode);
        }

        var lockedOutResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(email, "Password1!"));

        Assert.Equal(HttpStatusCode.Unauthorized, lockedOutResponse.StatusCode);
        var envelope = await lockedOutResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>(JsonOptions);
        Assert.NotNull(envelope);
        Assert.False(envelope.Success);
        Assert.Contains(envelope.Errors!, error => error.Code == "invalid_credentials");
    }

    [Fact]
    public async Task Refresh_RotatesToken_WhenRefreshTokenIsValid()
    {
        var auth = await RegisterAndGetAuthAsync();

        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new RefreshRequest(auth.RefreshToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>(JsonOptions);
        Assert.NotNull(envelope);
        Assert.True(envelope.Success);
        Assert.NotEqual(auth.RefreshToken, envelope.Data?.RefreshToken);
    }

    [Fact]
    public async Task Refresh_RejectsExpiredToken_WithInvalidRefreshTokenCode()
    {
        var auth = await RegisterAndGetAuthAsync();
        await ExpireRefreshTokenAsync(auth.RefreshToken);

        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new RefreshRequest(auth.RefreshToken));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>(JsonOptions);
        Assert.NotNull(envelope);
        Assert.False(envelope.Success);
        Assert.Contains(envelope.Errors!, error => error.Code == "invalid_refresh_token");
    }

    [Fact]
    public async Task Refresh_RejectsRevokedToken_WithInvalidRefreshTokenCode()
    {
        var auth = await RegisterAndGetAuthAsync();
        await RevokeRefreshTokenAsync(auth.RefreshToken);

        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new RefreshRequest(auth.RefreshToken));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>(JsonOptions);
        Assert.NotNull(envelope);
        Assert.False(envelope.Success);
        Assert.Contains(envelope.Errors!, error => error.Code == "invalid_refresh_token");
    }

    [Fact]
    public async Task Refresh_BlocksReuse_WhenOldRefreshTokenIsPresentedAgain()
    {
        var auth = await RegisterAndGetAuthAsync();
        var firstRefresh = await _client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new RefreshRequest(auth.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, firstRefresh.StatusCode);

        var reuseResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new RefreshRequest(auth.RefreshToken));

        Assert.Equal(HttpStatusCode.Unauthorized, reuseResponse.StatusCode);
        var envelope = await reuseResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>(JsonOptions);
        Assert.NotNull(envelope);
        Assert.False(envelope.Success);
        Assert.Contains(envelope.Errors!, error => error.Code == "refresh_token_reused");
    }

    [Fact]
    public async Task Me_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        var response = await _client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_ReturnsUser_WhenAccessTokenIsValid()
    {
        var auth = await RegisterAndGetAuthAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<UserDto>>(JsonOptions);
        Assert.NotNull(envelope);
        Assert.True(envelope.Success);
        Assert.Equal(auth.User.Email, envelope.Data?.Email);
    }

    private async Task<AuthResponseDto> RegisterAndGetAuthAsync()
    {
        var email = $"me-{Guid.NewGuid():N}@example.com";
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/register",
            CreateRegisterRequest(email));

        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>(JsonOptions);
        Assert.NotNull(envelope?.Data);
        return envelope.Data;
    }

    private static RegisterRequest CreateRegisterRequest(
        string email,
        string password = "Password1!",
        string fullName = "Test User",
        string? username = null) =>
        new(
            email,
            password,
            fullName,
            username ?? $"user_{Guid.NewGuid():N}");

    private async Task ExpireRefreshTokenAsync(string rawToken)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tokenHash = HashToken(rawToken);
        var storedToken = await context.RefreshTokens.SingleAsync(token => token.TokenHash == tokenHash);
        storedToken.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await context.SaveChangesAsync();
    }

    private async Task RevokeRefreshTokenAsync(string rawToken)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tokenHash = HashToken(rawToken);
        var storedToken = await context.RefreshTokens.SingleAsync(token => token.TokenHash == tokenHash);
        storedToken.RevokedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
    }

    private static string HashToken(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes);
    }
}
