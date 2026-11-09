using System.Net;
using Insaaf.IntegrationTests.Infrastructure;
using SkiaSharp;

namespace Insaaf.IntegrationTests;

/// <summary>Phase 11 on SQL Server: races on the new composite keys, the filtered report index, replies, avatars.</summary>
[Collection(SqlServerCollection.Name)]
public sealed class Phase11Tests
{
    private readonly SqlServerApiFactory _factory;
    private readonly Api _api;

    public Phase11Tests(SqlServerApiFactory factory)
    {
        _factory = factory;
        _api = new Api(factory);
    }

    [SqlServerFact]
    public async Task ParallelSavesAndCommentLikes_CountOnce()
    {
        var (restaurantId, reviewId, author, _) = await _api.CreateVerifiedReviewAsync();
        var user = await _api.RegisterAsync();
        var comment = await _api.SendAsync(HttpMethod.Post, $"/reviews/{reviewId}/comments", author.AccessToken, new { text = "تعليق" });
        var commentId = comment.Data.GetProperty("id").GetGuid();

        var saves = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => _api.SendAsync(HttpMethod.Put, $"/reviews/{reviewId}/save", user.AccessToken)));
        var restaurantSaves = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => _api.SendAsync(HttpMethod.Put, $"/restaurants/{restaurantId}/save", user.AccessToken)));
        var likes = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => _api.SendAsync(HttpMethod.Put, $"/reviews/{reviewId}/comments/{commentId}/like", user.AccessToken)));

        Assert.All(saves.Concat(restaurantSaves).Concat(likes), result => Assert.Equal(HttpStatusCode.OK, result.Status));
        Assert.Single((await _api.SendAsync(HttpMethod.Get, "/users/me/saved/reviews", user.AccessToken)).Data.GetProperty("items").EnumerateArray());
        Assert.Equal(1, (await _api.SendAsync(HttpMethod.Get, "/users/me/saved/restaurants", user.AccessToken)).Data.GetProperty("totalCount").GetInt32());
        var comments = await _api.SendAsync(HttpMethod.Get, $"/reviews/{reviewId}/comments", user.AccessToken);
        Assert.Equal(1, comments.Data.GetProperty("items")[0].GetProperty("likeCount").GetInt32());

        // Deleting the comment cascades its likes on SQL Server.
        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Delete, $"/reviews/{reviewId}/comments/{commentId}", author.AccessToken)).Status);
    }

    [SqlServerFact]
    public async Task ParallelReports_KeepOneOpenReport_TheFilteredIndexHolds()
    {
        var (_, reviewId, _, admin) = await _api.CreateVerifiedReviewAsync();
        var reporter = await _api.RegisterAsync();

        var results = await Task.WhenAll(Enumerable.Range(0, 5)
            .Select(_ => _api.SendAsync(HttpMethod.Post, $"/reviews/{reviewId}/report", reporter.AccessToken, new { reason = "offensive" })));

        Assert.All(results, result => Assert.Equal(HttpStatusCode.OK, result.Status));
        var queue = await _api.SendAsync(HttpMethod.Get, "/admin/reports?limit=50", admin.AccessToken);
        var reports = queue.Data.GetProperty("items").EnumerateArray().Where(item => item.GetProperty("reviewId").GetGuid() == reviewId).ToList();
        var report = Assert.Single(reports);

        // After resolving, a new report is allowed (the index only covers open reports).
        await _api.SendAsync(HttpMethod.Put, $"/admin/reports/{report.GetProperty("id").GetGuid()}/resolve", admin.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Post, $"/reviews/{reviewId}/report", reporter.AccessToken, new { reason = "other" })).Status);
    }

    [SqlServerFact]
    public async Task OfficialReply_AndAvatar_RoundTrip_OnSqlServer()
    {
        var (_, reviewId, author, admin) = await _api.CreateVerifiedReviewAsync();

        var parallel = await Task.WhenAll(Enumerable.Range(0, 3)
            .Select(i => _api.SendAsync(HttpMethod.Put, $"/admin/reviews/{reviewId}/reply", admin.AccessToken, new { text = $"رد {i}" })));
        Assert.All(parallel, result => Assert.Equal(HttpStatusCode.OK, result.Status));
        var detail = await _api.SendAsync(HttpMethod.Get, $"/reviews/{reviewId}", author.AccessToken);
        Assert.StartsWith("رد ", detail.Data.GetProperty("restaurantReply").GetProperty("text").GetString());
        var notifications = await _api.SendAsync(HttpMethod.Get, "/notifications", author.AccessToken);
        Assert.Single(notifications.Data.GetProperty("items").EnumerateArray(), item => item.GetProperty("type").GetString() == "reply");

        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Api.Jpeg(SKColors.Coral));
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        form.Add(file, "file", "avatar.jpg");
        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/users/me/avatar") { Content = form };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", author.AccessToken);
        var uploaded = await Api.ParseAsync(await _api.Client.SendAsync(request));
        Assert.Equal(HttpStatusCode.OK, uploaded.Status);
        var url = uploaded.Data.GetProperty("avatarUrl").GetString();
        Assert.Equal(HttpStatusCode.OK, (await _api.Client.GetAsync(url)).StatusCode);
        var withAvatar = await _api.SendAsync(HttpMethod.Get, $"/reviews/{reviewId}", author.AccessToken);
        Assert.Equal(url, withAvatar.Data.GetProperty("reviewer").GetProperty("avatarUrl").GetString());
    }

    [SqlServerFact]
    public async Task TasteSetup_FeedsSuggestedRestaurants_AndPasswordResetWorksOnSqlServer()
    {
        var admin = await _api.AdminAsync();
        await _api.CreatePartnerRestaurantAsync(admin);
        var user = await _api.RegisterAsync();

        var saved = await _api.SendAsync(HttpMethod.Put, "/users/me/taste-preferences", user.AccessToken, new
        {
            categorySlugs = new[] { await FirstSlugAsync() },
            priorities = new[] { "food_quality" },
        });
        Assert.Equal(HttpStatusCode.OK, saved.Status);
        Assert.True((await _api.SendAsync(HttpMethod.Get, "/users/me", user.AccessToken)).Data.GetProperty("hasCompletedTasteSetup").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Get, "/home/suggested", user.AccessToken)).Status);

        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Post, "/auth/forgot-password", null, new { email = user.Email })).Status);
        var mail = Directory.GetFiles(_factory.Outbox, "*.txt").Select(File.ReadAllText).Single(text => text.Contains($"To: {user.Email}"));
        var code = System.Text.RegularExpressions.Regex.Match(mail, @"\b\d{6}\b").Value;
        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Post, "/auth/reset-password", null, new { email = user.Email, code, newPassword = "Reset1!pass" })).Status);
        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Post, "/auth/login", null, new { email = user.Email, password = "Reset1!pass" })).Status);
    }

    private async Task<string> FirstSlugAsync()
    {
        await using var connection = new Microsoft.Data.SqlClient.SqlConnection(_factory.ConnectionString);
        await connection.OpenAsync();
        await using var command = new Microsoft.Data.SqlClient.SqlCommand("SELECT TOP 1 Slug FROM Categories ORDER BY Slug", connection);
        return (string)(await command.ExecuteScalarAsync())!;
    }
}
