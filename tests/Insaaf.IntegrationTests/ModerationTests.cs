using System.Net;
using Insaaf.IntegrationTests.Infrastructure;

namespace Insaaf.IntegrationTests;

/// <summary>Phase 9 on SQL Server: hide and unhide reviews, delete comments, deactivate restaurants.</summary>
[Collection(SqlServerCollection.Name)]
public sealed class ModerationTests
{
    private readonly Api _api;

    public ModerationTests(SqlServerApiFactory factory)
    {
        _api = new Api(factory);
    }

    [SqlServerFact]
    public async Task HidingAReview_RemovesItFromRatingsAndInteractions_UnhidingRestores()
    {
        var (restaurantId, reviewId, author, admin) = await _api.CreateVerifiedReviewAsync(rating: 5);
        var viewer = await _api.RegisterAsync();

        var hide = await _api.SendAsync(HttpMethod.Put, $"/admin/reviews/{reviewId}/visibility", admin.AccessToken, new { hidden = true, reason = "integration" });
        Assert.Equal(HttpStatusCode.OK, hide.Status);

        var hiddenRestaurant = await _api.SendAsync(HttpMethod.Get, $"/restaurants/{restaurantId}", viewer.AccessToken);
        Assert.Equal(0, hiddenRestaurant.Data.GetProperty("reviewCount").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await _api.SendAsync(HttpMethod.Get, $"/reviews/{reviewId}", viewer.AccessToken)).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await _api.SendAsync(HttpMethod.Put, $"/reviews/{reviewId}/like", viewer.AccessToken)).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await _api.SendAsync(HttpMethod.Post, $"/reviews/{reviewId}/comments", viewer.AccessToken, new { text = "x" })).Status);

        var adminList = await _api.SendAsync(HttpMethod.Get, $"/admin/reviews?restaurantId={restaurantId}", admin.AccessToken);
        var listed = Assert.Single(adminList.Data.GetProperty("items").EnumerateArray());
        Assert.True(listed.GetProperty("isHidden").GetBoolean());

        await _api.SendAsync(HttpMethod.Put, $"/admin/reviews/{reviewId}/visibility", admin.AccessToken, new { hidden = false });
        var restored = await _api.SendAsync(HttpMethod.Get, $"/restaurants/{restaurantId}", viewer.AccessToken);
        Assert.Equal(1, restored.Data.GetProperty("reviewCount").GetInt32());
        Assert.Equal(5, restored.Data.GetProperty("averageRating").GetDouble());
        var detail = await _api.SendAsync(HttpMethod.Get, $"/reviews/{reviewId}", author.AccessToken);
        Assert.Equal(HttpStatusCode.OK, detail.Status);
        Assert.DoesNotContain("integration", detail.Raw);
    }

    [SqlServerFact]
    public async Task AdminDeletesAComment_AndADeactivatedRestaurantIsHiddenFromThePublic()
    {
        var (restaurantId, reviewId, author, admin) = await _api.CreateVerifiedReviewAsync();
        var comment = await _api.SendAsync(HttpMethod.Post, $"/reviews/{reviewId}/comments", author.AccessToken, new { text = "تعليق" });
        var commentId = comment.Data.GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Delete, $"/admin/comments/{commentId}", admin.AccessToken)).Status);
        var left = await _api.SendAsync(HttpMethod.Get, $"/reviews/{reviewId}/comments", author.AccessToken);
        Assert.Equal(0, left.Data.GetProperty("items").GetArrayLength());

        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Put, $"/admin/restaurants/{restaurantId}/status", admin.AccessToken, new { isActive = false })).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await _api.SendAsync(HttpMethod.Get, $"/restaurants/{restaurantId}", author.AccessToken)).Status);
    }

    [SqlServerFact]
    public async Task UserIsForbiddenOnAdminRoutes()
    {
        var user = await _api.RegisterAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await _api.SendAsync(HttpMethod.Get, "/admin/restaurants", user.AccessToken)).Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.SendAsync(HttpMethod.Get, "/admin/restaurants")).Status);
    }
}
