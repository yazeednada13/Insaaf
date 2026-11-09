using System.Net;
using System.Net.Http.Json;
using Insaaf.API.Extensions;
using Insaaf.Application.Auth.Dtos;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Insaaf.API.Tests;

/// <summary>Phase 10, task 10.1: auth rate limits (F-3), the signing-key guard (F-7), security headers (F-8).</summary>
public sealed class SecurityHardeningTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public SecurityHardeningTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Responses_CarrySecurityHeaders()
    {
        var response = await _factory.CreateClient().GetAsync("/api/v1/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Contains("frame-ancestors 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
    }

    [Theory]
    [InlineData("DevelopmentOnlySigningKey_ChangeViaUserSecrets_32chars!")]
    [InlineData("short-key")]
    [InlineData("")]
    public void SigningKeyGuard_RefusesWeakOrDevelopmentKeys_OutsideDevelopment(string key)
    {
        var configuration = Configuration(key);

        Assert.Throws<InvalidOperationException>(() =>
            SecurityExtensions.EnsureProductionSigningKey(configuration, new FakeEnvironment("Production")));
        SecurityExtensions.EnsureProductionSigningKey(configuration, new FakeEnvironment("Development"));
    }

    [Fact]
    public void SigningKeyGuard_AcceptsAStrongPrivateKey()
    {
        var configuration = Configuration(Convert.ToBase64String(Guid.NewGuid().ToByteArray().Concat(Guid.NewGuid().ToByteArray()).ToArray()));

        SecurityExtensions.EnsureProductionSigningKey(configuration, new FakeEnvironment("Production"));
    }

    private static IConfiguration Configuration(string key) =>
        new ConfigurationBuilder().AddInMemoryCollection([new KeyValuePair<string, string?>("Jwt:SigningKey", key)]).Build();

    private sealed class FakeEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = "Insaaf.API";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

/// <summary>A host with low auth limits, so the 429 responses can be observed.</summary>
public sealed class LowAuthRateLimitWebApplicationFactory : CustomWebApplicationFactory
{
    protected override void ConfigureTestSettings(IWebHostBuilder builder)
    {
        builder.UseSetting("RateLimiting:AuthLoginPerMinute", "2");
        builder.UseSetting("RateLimiting:AuthRegisterPerMinute", "2");
        builder.UseSetting("RateLimiting:AuthRefreshPerMinute", "2");
    }
}

public sealed class AuthRateLimitTests : IClassFixture<LowAuthRateLimitWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthRateLimitTests(LowAuthRateLimitWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Login_Register_AndRefresh_AreRateLimited()
    {
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++)
        {
            statuses.Add((await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("nobody@example.com", "Wrong1pass"))).StatusCode);
        }

        var registrations = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++)
        {
            var id = Guid.NewGuid().ToString("N");
            registrations.Add((await _client.PostAsJsonAsync(
                "/api/v1/auth/register",
                new RegisterRequest($"limit-{id}@example.com", "Password1!", "Limit", $"limit_{id[..12]}"))).StatusCode);
        }

        var refreshes = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++)
        {
            refreshes.Add((await _client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest("not-a-token"))).StatusCode);
        }

        Assert.Equal([HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests], statuses);
        Assert.Equal(HttpStatusCode.TooManyRequests, registrations[2]);
        Assert.All(registrations.Take(2), status => Assert.NotEqual(HttpStatusCode.TooManyRequests, status));
        Assert.Equal(HttpStatusCode.TooManyRequests, refreshes[2]);
    }
}
