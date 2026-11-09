using System.Net;
using Insaaf.IntegrationTests.Infrastructure;
using SkiaSharp;
using Xunit.Abstractions;

namespace Insaaf.IntegrationTests;

/// <summary>
/// Phase 10 (10.5): no N+1 on the main reads. Each read is measured with one reviewer, then again after three
/// more reviewers (each with a photo, a like, a comment and follows) were added. The command count must not grow.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class QueryCountTests
{
    private readonly SqlServerApiFactory _factory;
    private readonly Api _api;
    private readonly ITestOutputHelper _output;

    public QueryCountTests(SqlServerApiFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _api = new Api(factory);
        _output = output;
    }

    [SqlServerFact]
    public async Task MainReads_UseTheSameNumberOfCommands_AsDataGrows()
    {
        var admin = await _api.AdminAsync();
        var restaurantId = await _api.CreatePartnerRestaurantAsync(admin);
        var tokens = await _api.IssueTokensAsync(admin, restaurantId, 5);
        var viewer = await _api.RegisterAsync();

        var first = await AddReviewerAsync(viewer, tokens[0], null);
        var small = await MeasureAsync(viewer, admin, restaurantId, first);

        for (var i = 1; i <= 3; i++)
        {
            await AddReviewerAsync(viewer, tokens[i], first);
        }

        // The first reviewer also writes a second review, so their own lists grow too.
        Assert.Equal(HttpStatusCode.Created, (await _api.CreateReviewAsync(first.User, tokens[4], 3)).Status);
        var large = await MeasureAsync(viewer, admin, restaurantId, first);

        _output.WriteLine($"{"read",-36} {"1 reviewer",10} {"4 reviewers",12}");
        foreach (var (read, count) in small)
        {
            _output.WriteLine($"{read,-36} {count,10} {large[read],12}");
        }

        Assert.All(small, entry => Assert.True(
            large[entry.Key] <= entry.Value,
            $"{entry.Key}: {entry.Value} commands with 1 reviewer, {large[entry.Key]} with 4 (grows with data)."));
    }

    private async Task<Reviewer> AddReviewerAsync(TestUser viewer, string token, Reviewer? followee)
    {
        var user = await _api.RegisterAsync();
        var photo = await _api.UploadPhotoAsync(user, token, Guid.NewGuid(), Api.Jpeg(SKColors.Orange));
        var review = await _api.CreateReviewAsync(user, token, 4, [photo.Data.GetProperty("id").GetGuid()]);
        Assert.Equal(HttpStatusCode.Created, review.Status);
        var reviewId = review.Data.GetProperty("reviewId").GetGuid();

        await _api.SendAsync(HttpMethod.Put, $"/reviews/{reviewId}/like", viewer.AccessToken);
        await _api.SendAsync(HttpMethod.Post, $"/reviews/{reviewId}/comments", viewer.AccessToken, new { text = "تعليق" });
        await _api.SendAsync(HttpMethod.Put, $"/users/{user.Id}/follow", viewer.AccessToken);
        await _api.SendAsync(HttpMethod.Put, $"/users/{viewer.Id}/follow", user.AccessToken);
        if (followee is not null)
        {
            await _api.SendAsync(HttpMethod.Put, $"/users/{followee.User.Id}/follow", user.AccessToken);
            await _api.SendAsync(HttpMethod.Put, $"/reviews/{followee.ReviewId}/like", user.AccessToken);
            await _api.SendAsync(HttpMethod.Post, $"/reviews/{followee.ReviewId}/comments", user.AccessToken, new { text = "تعليق" });
        }

        return new Reviewer(user, reviewId);
    }

    private async Task<Dictionary<string, int>> MeasureAsync(TestUser viewer, TestUser admin, Guid restaurantId, Reviewer first)
    {
        var reads = new (string Name, string Path, TestUser As)[]
        {
            ("GET /restaurants", "/restaurants?pageSize=20", viewer),
            ("GET /restaurants/{id}", $"/restaurants/{restaurantId}", viewer),
            ("GET /restaurants/{id}/reviews", $"/restaurants/{restaurantId}/reviews", viewer),
            ("GET /reviews/{id}", $"/reviews/{first.ReviewId}", viewer),
            ("GET /reviews/{id}/comments", $"/reviews/{first.ReviewId}/comments", viewer),
            ("GET /feed", "/feed", viewer),
            ("GET /notifications", "/notifications", viewer),
            ("GET /notifications/unread-count", "/notifications/unread-count", viewer),
            ("GET /users/{id}", $"/users/{first.User.Id}", viewer),
            ("GET /users/{id}/reviews", $"/users/{first.User.Id}/reviews", viewer),
            ("GET /users/{id}/followers", $"/users/{first.User.Id}/followers", viewer),
            ("GET /users/{id}/taste", $"/users/{first.User.Id}/taste", viewer),
            ("GET /home/suggested", "/home/suggested", viewer),
            ("GET /home/trending", "/home/trending", viewer),
            ("GET /admin/reviews", $"/admin/reviews?restaurantId={restaurantId}", admin),
        };

        var counts = new Dictionary<string, int>();
        foreach (var (name, path, user) in reads)
        {
            _factory.Commands.Reset();
            var result = await _api.SendAsync(HttpMethod.Get, path, user.AccessToken);
            Assert.True(result.Status == HttpStatusCode.OK, $"{name}: {result.Status} {result.Raw}");
            counts[name] = _factory.Commands.Count;
        }

        return counts;
    }

    private sealed record Reviewer(TestUser User, Guid ReviewId);
}
