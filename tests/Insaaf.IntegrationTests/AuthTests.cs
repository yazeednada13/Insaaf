using System.Net;
using Insaaf.IntegrationTests.Infrastructure;

namespace Insaaf.IntegrationTests;

/// <summary>Phases 2, 8 and 9 on SQL Server: refresh rotation, sign-out, unique usernames, suspension.</summary>
[Collection(SqlServerCollection.Name)]
public sealed class AuthTests
{
    private readonly Api _api;

    public AuthTests(SqlServerApiFactory factory)
    {
        _api = new Api(factory);
    }

    [SqlServerFact]
    public async Task Refresh_Rotates_AndAReusedTokenIsRefused()
    {
        var user = await _api.RegisterAsync();

        var rotated = await _api.SendAsync(HttpMethod.Post, "/auth/refresh", null, new { refreshToken = user.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, rotated.Status);
        var next = rotated.Data.GetProperty("refreshToken").GetString();
        Assert.NotEqual(user.RefreshToken, next);

        var reused = await _api.SendAsync(HttpMethod.Post, "/auth/refresh", null, new { refreshToken = user.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, reused.Status);
        Assert.Equal("refresh_token_reused", reused.ErrorCode);
    }

    [SqlServerFact]
    public async Task Logout_RevokesTheRefreshToken_AndIsRepeatSafe()
    {
        var user = await _api.RegisterAsync();

        var first = await _api.SendAsync(HttpMethod.Post, "/auth/logout", user.AccessToken);
        var second = await _api.SendAsync(HttpMethod.Post, "/auth/logout", user.AccessToken);
        var refresh = await _api.SendAsync(HttpMethod.Post, "/auth/refresh", null, new { refreshToken = user.RefreshToken });

        Assert.Equal(HttpStatusCode.OK, first.Status);
        Assert.Equal(HttpStatusCode.OK, second.Status);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.Status);
    }

    [SqlServerFact]
    public async Task Username_IsUniqueRegardlessOfCase()
    {
        var a = await _api.RegisterAsync();
        var b = await _api.RegisterAsync();
        var name = $"ItName_{Guid.NewGuid():N}"[..20];

        var set = await _api.SendAsync(HttpMethod.Put, "/users/me", a.AccessToken, new { fullName = "أ", userName = name, bio = "نبذة" });
        var clash = await _api.SendAsync(HttpMethod.Put, "/users/me", b.AccessToken, new { fullName = "ب", userName = name.ToLowerInvariant() });
        var ownCase = await _api.SendAsync(HttpMethod.Put, "/users/me", a.AccessToken, new { fullName = "أ", userName = name.ToLowerInvariant(), bio = "نبذة" });

        Assert.Equal(HttpStatusCode.OK, set.Status);
        Assert.Equal("نبذة", set.Data.GetProperty("bio").GetString());
        Assert.Equal(HttpStatusCode.Conflict, clash.Status);
        Assert.Equal("username_taken", clash.ErrorCode);
        Assert.Equal(HttpStatusCode.OK, ownCase.Status);
        Assert.Equal(HttpStatusCode.OK, (await _api.LoginAsync(a.Email)).Status);
    }

    [SqlServerFact]
    public async Task Suspension_BlocksSignInAndRefresh_UntilLifted()
    {
        var admin = await _api.AdminAsync();
        var user = await _api.RegisterAsync();

        var suspend = await _api.SendAsync(HttpMethod.Put, $"/admin/users/{user.Id}/suspension", admin.AccessToken, new { suspended = true });
        var login = await _api.LoginAsync(user.Email);
        var refresh = await _api.SendAsync(HttpMethod.Post, "/auth/refresh", null, new { refreshToken = user.RefreshToken });
        // Documented (ADR-017 / ADR-020): the current access token keeps working until it expires.
        var me = await _api.SendAsync(HttpMethod.Get, "/users/me", user.AccessToken);

        Assert.Equal(HttpStatusCode.OK, suspend.Status);
        Assert.Equal(HttpStatusCode.Forbidden, login.Status);
        Assert.Equal("account_suspended", login.ErrorCode);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.Status);
        Assert.Equal(HttpStatusCode.OK, me.Status);

        var lift = await _api.SendAsync(HttpMethod.Put, $"/admin/users/{user.Id}/suspension", admin.AccessToken, new { suspended = false });
        Assert.Equal(HttpStatusCode.OK, lift.Status);
        Assert.Equal(HttpStatusCode.OK, (await _api.LoginAsync(user.Email)).Status);
    }
}
