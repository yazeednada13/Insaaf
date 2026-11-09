using System.Net;
using System.Net.Http.Headers;
using Insaaf.IntegrationTests.Infrastructure;
using SkiaSharp;

namespace Insaaf.IntegrationTests;

/// <summary>Phase 9A on SQL Server, over HTTP: upload, idempotency, limits, attach, serve, moderate.</summary>
[Collection(SqlServerCollection.Name)]
public sealed class PhotoTests
{
    private readonly SqlServerApiFactory _factory;
    private readonly Api _api;

    public PhotoTests(SqlServerApiFactory factory)
    {
        _factory = factory;
        _api = new Api(factory);
    }

    [SqlServerFact]
    public async Task UploadLimitsClaimAndIdempotency()
    {
        var admin = await _api.AdminAsync();
        var restaurantId = await _api.CreatePartnerRestaurantAsync(admin);
        var token = (await _api.IssueTokensAsync(admin, restaurantId, 1)).Single();
        var a = await _api.RegisterAsync();
        var b = await _api.RegisterAsync();

        var uploadId = Guid.NewGuid();
        var first = await _api.UploadPhotoAsync(a, token, uploadId, Api.Jpeg(SKColors.Orange));
        var repeat = await _api.UploadPhotoAsync(a, token, uploadId, Api.Jpeg(SKColors.Orange));
        Assert.Equal(HttpStatusCode.Created, first.Status);
        Assert.Equal(HttpStatusCode.OK, repeat.Status);
        Assert.Equal(first.Data.GetProperty("id").GetGuid(), repeat.Data.GetProperty("id").GetGuid());

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, (await _api.UploadPhotoAsync(a, token, Guid.NewGuid(), "not an image"u8.ToArray())).Status);

        var claimed = await _api.UploadPhotoAsync(b, token, Guid.NewGuid(), Api.Jpeg(SKColors.Blue));
        Assert.Equal(HttpStatusCode.Forbidden, claimed.Status);
        Assert.Equal("token_claimed", claimed.ErrorCode);

        var ids = new List<Guid> { first.Data.GetProperty("id").GetGuid() };
        for (var i = 0; i < 4; i++)
        {
            ids.Add((await _api.UploadPhotoAsync(a, token, Guid.NewGuid(), Api.Jpeg(SKColors.Green))).Data.GetProperty("id").GetGuid());
        }

        var sixth = await _api.UploadPhotoAsync(a, token, Guid.NewGuid(), Api.Jpeg(SKColors.Red));
        Assert.Equal(HttpStatusCode.Conflict, sixth.Status);
        Assert.Equal("photo_limit", sixth.ErrorCode);

        var deleted = await _api.SendAsync(HttpMethod.Delete, $"/verification/photos/{ids[4]}", a.AccessToken);
        Assert.Equal(HttpStatusCode.OK, deleted.Status);
        Assert.Equal("completed", deleted.Data.GetProperty("fileDeletion").GetString());
        // The freed slot can be used again.
        Assert.Equal(HttpStatusCode.Created, (await _api.UploadPhotoAsync(a, token, Guid.NewGuid(), Api.Jpeg(SKColors.Gray))).Status);
    }

    [SqlServerFact]
    public async Task AttachedPhotos_AreServedInOrder_WithCaching_AndModerationHidesThem()
    {
        var admin = await _api.AdminAsync();
        var restaurantId = await _api.CreatePartnerRestaurantAsync(admin);
        var tokens = await _api.IssueTokensAsync(admin, restaurantId, 2);
        var author = await _api.RegisterAsync();

        var p1 = (await _api.UploadPhotoAsync(author, tokens[0], Guid.NewGuid(), Api.Jpeg(SKColors.Orange))).Data.GetProperty("id").GetGuid();
        var p2 = (await _api.UploadPhotoAsync(author, tokens[0], Guid.NewGuid(), Api.Jpeg(SKColors.Teal))).Data.GetProperty("id").GetGuid();
        var foreign = (await _api.UploadPhotoAsync(author, tokens[1], Guid.NewGuid(), Api.Jpeg(SKColors.Purple))).Data.GetProperty("id").GetGuid();

        // Another token's photo is refused and the token stays usable.
        var invalid = await _api.CreateReviewAsync(author, tokens[0], 5, [p1, foreign]);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.Status);
        Assert.Equal("invalid_photo", invalid.ErrorCode);
        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Post, "/verification/visits/resolve", author.AccessToken, new { token = tokens[0] })).Status);

        var review = await _api.CreateReviewAsync(author, tokens[0], 4, [p2, p1]);
        Assert.Equal(HttpStatusCode.Created, review.Status);
        var reviewId = review.Data.GetProperty("reviewId").GetGuid();

        var detail = await _api.SendAsync(HttpMethod.Get, $"/reviews/{reviewId}", author.AccessToken);
        var order = detail.Data.GetProperty("photos").EnumerateArray().Select(photo => photo.GetProperty("id").GetGuid()).ToArray();
        Assert.Equal([p2, p1], order);
        Assert.DoesNotContain(_factory.PhotoRoot, detail.Raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("storageKey", detail.Raw, StringComparison.OrdinalIgnoreCase);

        var image = await _api.Client.GetAsync($"/api/v1/review-photos/{p1}");
        Assert.Equal(HttpStatusCode.OK, image.StatusCode);
        Assert.Equal("image/jpeg", image.Content.Headers.ContentType?.MediaType);
        Assert.True(image.Headers.CacheControl?.Private);
        Assert.True(image.Headers.CacheControl?.NoCache);
        using (var revalidate = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/review-photos/{p1}"))
        {
            revalidate.Headers.IfNoneMatch.Add(image.Headers.ETag!);
            Assert.Equal(HttpStatusCode.NotModified, (await _api.Client.SendAsync(revalidate)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.NotFound, (await _api.Client.GetAsync($"/api/v1/review-photos/{foreign}")).StatusCode);

        // Phase 10 (D-7): cards use the thumbnail URL from the response.
        var thumbnailUrl = detail.Data.GetProperty("photos")[0].GetProperty("thumbnailUrl").GetString();
        Assert.Equal($"/api/v1/review-photos/{p2}?size=thumb", thumbnailUrl);
        var thumbnail = await _api.Client.GetAsync(thumbnailUrl);
        Assert.Equal(HttpStatusCode.OK, thumbnail.StatusCode);
        Assert.NotEqual(image.Headers.ETag, thumbnail.Headers.ETag);

        var removed = await _api.SendAsync(HttpMethod.Delete, $"/admin/photos/{p1}", admin.AccessToken);
        Assert.Equal("completed", removed.Data.GetProperty("fileDeletion").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await _api.Client.GetAsync($"/api/v1/review-photos/{p1}")).StatusCode);
        var afterRemoval = await _api.SendAsync(HttpMethod.Get, $"/reviews/{reviewId}", author.AccessToken);
        Assert.Equal(1, afterRemoval.Data.GetProperty("photos").GetArrayLength());

        await _api.SendAsync(HttpMethod.Put, $"/admin/reviews/{reviewId}/visibility", admin.AccessToken, new { hidden = true });
        Assert.Equal(HttpStatusCode.NotFound, (await _api.Client.GetAsync($"/api/v1/review-photos/{p2}")).StatusCode);
        await _api.SendAsync(HttpMethod.Put, $"/admin/reviews/{reviewId}/visibility", admin.AccessToken, new { hidden = false });
        Assert.Equal(HttpStatusCode.OK, (await _api.Client.GetAsync($"/api/v1/review-photos/{p2}")).StatusCode);
    }
}
