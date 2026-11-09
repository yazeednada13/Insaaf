using System.Net;
using Insaaf.IntegrationTests.Infrastructure;

namespace Insaaf.IntegrationTests;

/// <summary>Phases 6–8A on SQL Server: parallel likes and follows, comments, feed paging, profiles, notifications.</summary>
[Collection(SqlServerCollection.Name)]
public sealed class SocialTests
{
    private readonly Api _api;

    public SocialTests(SqlServerApiFactory factory)
    {
        _api = new Api(factory);
    }

    [SqlServerFact]
    public async Task FiveParallelLikesByOneUser_CountOnce_AndRatingsDontMove()
    {
        var (restaurantId, reviewId, author, _) = await _api.CreateVerifiedReviewAsync();
        var liker = await _api.RegisterAsync();
        var before = await _api.SendAsync(HttpMethod.Get, $"/restaurants/{restaurantId}", liker.AccessToken);

        var likes = await Task.WhenAll(Enumerable.Range(0, 5)
            .Select(_ => _api.SendAsync(HttpMethod.Put, $"/reviews/{reviewId}/like", liker.AccessToken)));

        Assert.All(likes, like => Assert.Equal(HttpStatusCode.OK, like.Status));
        var detail = await _api.SendAsync(HttpMethod.Get, $"/reviews/{reviewId}", liker.AccessToken);
        Assert.Equal(1, detail.Data.GetProperty("likeCount").GetInt32());
        Assert.True(detail.Data.GetProperty("isLikedByMe").GetBoolean());

        var unlike = await _api.SendAsync(HttpMethod.Delete, $"/reviews/{reviewId}/like", liker.AccessToken);
        var again = await _api.SendAsync(HttpMethod.Delete, $"/reviews/{reviewId}/like", liker.AccessToken);
        Assert.Equal(0, again.Data.GetProperty("likeCount").GetInt32());
        Assert.Equal(HttpStatusCode.OK, unlike.Status);

        var after = await _api.SendAsync(HttpMethod.Get, $"/restaurants/{restaurantId}", author.AccessToken);
        Assert.Equal(before.Data.GetProperty("averageRating").GetDouble(), after.Data.GetProperty("averageRating").GetDouble());
        Assert.Equal(before.Data.GetProperty("reviewCount").GetInt32(), after.Data.GetProperty("reviewCount").GetInt32());
    }

    [SqlServerFact]
    public async Task Comments_AreTrimmed_AndOnlyTheOwnerDeletes()
    {
        var (_, reviewId, author, _) = await _api.CreateVerifiedReviewAsync();
        var other = await _api.RegisterAsync();

        var created = await _api.SendAsync(HttpMethod.Post, $"/reviews/{reviewId}/comments", other.AccessToken, new { text = "  تعليق تكامل  " });
        Assert.Equal(HttpStatusCode.Created, created.Status);
        Assert.Equal("تعليق تكامل", created.Data.GetProperty("text").GetString());
        var commentId = created.Data.GetProperty("id").GetGuid();

        var forbidden = await _api.SendAsync(HttpMethod.Delete, $"/reviews/{reviewId}/comments/{commentId}", author.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.Status);
        Assert.Equal("comment_forbidden", forbidden.ErrorCode);

        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Delete, $"/reviews/{reviewId}/comments/{commentId}", other.AccessToken)).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await _api.SendAsync(HttpMethod.Delete, $"/reviews/{reviewId}/comments/{commentId}", other.AccessToken)).Status);
    }

    [SqlServerFact]
    public async Task ParallelFollows_CountOnce_AndTheFeedPagesWithoutRepeats()
    {
        var admin = await _api.AdminAsync();
        var restaurantId = await _api.CreatePartnerRestaurantAsync(admin);
        var tokens = await _api.IssueTokensAsync(admin, restaurantId, 3);
        var reviewer = await _api.RegisterAsync();
        foreach (var token in tokens)
        {
            Assert.Equal(HttpStatusCode.Created, (await _api.CreateReviewAsync(reviewer, token, 4)).Status);
        }

        var viewer = await _api.RegisterAsync();
        var empty = await _api.SendAsync(HttpMethod.Get, "/feed", viewer.AccessToken);
        Assert.Equal(0, empty.Data.GetProperty("items").GetArrayLength());

        var follows = await Task.WhenAll(Enumerable.Range(0, 5)
            .Select(_ => _api.SendAsync(HttpMethod.Put, $"/users/{reviewer.Id}/follow", viewer.AccessToken)));
        Assert.All(follows, follow => Assert.Equal(HttpStatusCode.OK, follow.Status));
        var profile = await _api.SendAsync(HttpMethod.Get, $"/users/{reviewer.Id}", viewer.AccessToken);
        Assert.Equal(1, profile.Data.GetProperty("followerCount").GetInt32());
        Assert.True(profile.Data.GetProperty("isFollowedByMe").GetBoolean());
        Assert.DoesNotContain("example.com", profile.Raw);

        var seen = new List<Guid>();
        string? cursor = null;
        do
        {
            var page = await _api.SendAsync(HttpMethod.Get, "/feed?limit=1" + (cursor is null ? "" : $"&cursor={Uri.EscapeDataString(cursor)}"), viewer.AccessToken);
            Assert.Equal(HttpStatusCode.OK, page.Status);
            seen.AddRange(page.Data.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()));
            var next = page.Data.GetProperty("nextCursor");
            cursor = next.ValueKind == System.Text.Json.JsonValueKind.Null ? null : next.GetString();
        }
        while (cursor is not null && seen.Count < 10);

        Assert.Equal(3, seen.Count);
        Assert.Equal(3, seen.Distinct().Count());

        await _api.SendAsync(HttpMethod.Delete, $"/users/{reviewer.Id}/follow", viewer.AccessToken);
        var afterUnfollow = await _api.SendAsync(HttpMethod.Get, "/feed", viewer.AccessToken);
        Assert.Equal(0, afterUnfollow.Data.GetProperty("items").GetArrayLength());
    }

    [SqlServerFact]
    public async Task Notifications_FollowNotifiesOnce_ReadAll_AndUnfollowRemoves()
    {
        var a = await _api.RegisterAsync();
        var b = await _api.RegisterAsync();

        await _api.SendAsync(HttpMethod.Put, $"/users/{a.Id}/follow", b.AccessToken);
        await _api.SendAsync(HttpMethod.Put, $"/users/{a.Id}/follow", b.AccessToken);

        var list = await _api.SendAsync(HttpMethod.Get, "/notifications", a.AccessToken);
        var item = Assert.Single(list.Data.GetProperty("items").EnumerateArray());
        Assert.Equal("follow", item.GetProperty("type").GetString());
        Assert.Equal(b.Id, item.GetProperty("actor").GetProperty("id").GetGuid());
        Assert.DoesNotContain("example.com", list.Raw);
        Assert.Equal(1, (await _api.SendAsync(HttpMethod.Get, "/notifications/unread-count", a.AccessToken)).Data.GetProperty("count").GetInt32());
        Assert.Equal(0, (await _api.SendAsync(HttpMethod.Get, "/notifications", b.AccessToken)).Data.GetProperty("items").GetArrayLength());

        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Post, "/notifications/read-all", a.AccessToken)).Status);
        Assert.Equal(0, (await _api.SendAsync(HttpMethod.Get, "/notifications/unread-count", a.AccessToken)).Data.GetProperty("count").GetInt32());

        await _api.SendAsync(HttpMethod.Delete, $"/users/{a.Id}/follow", b.AccessToken);
        Assert.Equal(0, (await _api.SendAsync(HttpMethod.Get, "/notifications", a.AccessToken)).Data.GetProperty("items").GetArrayLength());
    }
}
