using System.Net;
using Insaaf.IntegrationTests.Infrastructure;

namespace Insaaf.IntegrationTests;

/// <summary>Phases 4–5 on SQL Server: QR token → verified review → server-resolved restaurant and ratings.</summary>
[Collection(SqlServerCollection.Name)]
public sealed class VerificationTests
{
    private readonly Api _api;

    public VerificationTests(SqlServerApiFactory factory)
    {
        _api = new Api(factory);
    }

    [SqlServerFact]
    public async Task VerifiedReviews_UpdateTheRestaurantRating_AndATokenIsSingleUse()
    {
        var admin = await _api.AdminAsync();
        var restaurantId = await _api.CreatePartnerRestaurantAsync(admin);
        var tokens = await _api.IssueTokensAsync(admin, restaurantId, 2);
        var a = await _api.RegisterAsync();
        var b = await _api.RegisterAsync();

        var resolved = await _api.SendAsync(HttpMethod.Post, "/verification/visits/resolve", a.AccessToken, new { token = tokens[0] });
        Assert.Equal(HttpStatusCode.OK, resolved.Status);
        Assert.Contains(restaurantId.ToString(), resolved.Raw, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(HttpStatusCode.Created, (await _api.CreateReviewAsync(a, tokens[0], 5)).Status);
        Assert.Equal(HttpStatusCode.Created, (await _api.CreateReviewAsync(b, tokens[1], 2)).Status);

        var reused = await _api.CreateReviewAsync(b, tokens[0], 1);
        Assert.Equal(HttpStatusCode.Conflict, reused.Status);
        Assert.Equal("token_used", reused.ErrorCode);

        var restaurant = await _api.SendAsync(HttpMethod.Get, $"/restaurants/{restaurantId}", a.AccessToken);
        Assert.Equal(2, restaurant.Data.GetProperty("reviewCount").GetInt32());
        Assert.Equal(3.5, restaurant.Data.GetProperty("averageRating").GetDouble(), precision: 2);

        var reviews = await _api.SendAsync(HttpMethod.Get, $"/restaurants/{restaurantId}/reviews", a.AccessToken);
        Assert.Equal(2, reviews.Data.GetProperty("items").GetArrayLength());
    }

    [SqlServerFact]
    public async Task FiveParallelRedemptionsOfOneToken_CreateExactlyOneReview()
    {
        var admin = await _api.AdminAsync();
        var restaurantId = await _api.CreatePartnerRestaurantAsync(admin);
        var token = (await _api.IssueTokensAsync(admin, restaurantId, 1)).Single();
        var users = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => _api.RegisterAsync()));

        var results = await Task.WhenAll(users.Select(user => _api.CreateReviewAsync(user, token, 4)));

        Assert.Single(results, result => result.Status == HttpStatusCode.Created);
        Assert.All(
            results.Where(result => result.Status != HttpStatusCode.Created),
            result => Assert.Equal(HttpStatusCode.Conflict, result.Status));
        var restaurant = await _api.SendAsync(HttpMethod.Get, $"/restaurants/{restaurantId}", users[0].AccessToken);
        Assert.Equal(1, restaurant.Data.GetProperty("reviewCount").GetInt32());
    }
}
