using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Insaaf.Application.Common.Models;
using Insaaf.Application.Photos.Dtos;
using Insaaf.Application.Reviews.Dtos;
using Insaaf.Application.Social.Dtos;
using Insaaf.Application.Verification.Dtos;
using Insaaf.Domain.Entities;
using Insaaf.Domain.Enums;
using Insaaf.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;

namespace Insaaf.API.Tests;

/// <summary>Phase 9A: photo upload, attachment, serving and removal over HTTP, on the real QR flow.</summary>
public sealed class PhotoEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly SocialApi _api;

    public PhotoEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _api = new SocialApi(factory, _client);
    }

    [Fact]
    public async Task PhotoRoutes_RequireTheRightAccess()
    {
        var id = Guid.NewGuid();
        var user = await _api.RegisterAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.PostAsync("/api/v1/verification/photos", Form("x", Guid.NewGuid(), Jpeg()))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.DeleteAsync($"/api/v1/verification/photos/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.DeleteAsync($"/api/v1/admin/photos/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _api.SendAsync(HttpMethod.Delete, $"/api/v1/admin/photos/{id}", user)).StatusCode);
        // The image route is anonymous but only serves attached photos of public reviews.
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/v1/review-photos/{id}")).StatusCode);
    }

    [Fact]
    public async Task UploadAttachServeAndRemove_OnAQrCreatedReview()
    {
        var token = await IssueTokenAsync();
        var author = await _api.RegisterAsync();
        var uploadId = Guid.NewGuid();

        var created = await UploadAsync(author, token, uploadId, Jpeg());
        var repeated = await UploadAsync(author, token, uploadId, Jpeg());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
        var photo = await ReadAsync<UploadedPhotoDto>(created);
        Assert.Equal(photo.Id, (await ReadAsync<UploadedPhotoDto>(repeated)).Id);

        // Not attached yet: not served.
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/v1/review-photos/{photo.Id}")).StatusCode);

        var review = await _api.SendAsync(HttpMethod.Post, "/api/v1/verification/reviews", author, new
        {
            token, rating = 5, foodRating = 5, serviceRating = 4, valueRating = 4, atmosphereRating = 5, text = "مع صورة",
            photoIds = new[] { photo.Id },
        });
        Assert.Equal(HttpStatusCode.Created, review.StatusCode);
        var reviewId = (await ReadAsync<VerifiedReviewCreatedDto>(review)).ReviewId;

        var detailResponse = await _api.SendAsync(HttpMethod.Get, $"/api/v1/reviews/{reviewId}", author);
        var raw = await detailResponse.Content.ReadAsStringAsync();
        var detail = JsonSerializer.Deserialize<ApiResponse<ReviewDetailDto>>(raw, JsonOptions)!.Data!;
        Assert.Equal($"/api/v1/review-photos/{photo.Id}", Assert.Single(detail.Photos).Url);
        Assert.DoesNotContain(_factory.PhotoRoot, raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("storageKey", raw, StringComparison.OrdinalIgnoreCase);

        var image = await _client.GetAsync($"/api/v1/review-photos/{photo.Id}");
        Assert.Equal(HttpStatusCode.OK, image.StatusCode);
        Assert.Equal("image/jpeg", image.Content.Headers.ContentType!.MediaType);
        Assert.True(image.Headers.CacheControl!.Private);
        Assert.True(image.Headers.CacheControl.NoCache);
        Assert.Contains("nosniff", image.Headers.GetValues("X-Content-Type-Options"));
        var etag = image.Headers.ETag!;
        var revalidate = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/review-photos/{photo.Id}");
        revalidate.Headers.IfNoneMatch.Add(etag);
        Assert.Equal(HttpStatusCode.NotModified, (await _client.SendAsync(revalidate)).StatusCode);

        var admin = await _api.AdminTokenAsync();
        var removed = await _api.SendAsync(HttpMethod.Delete, $"/api/v1/admin/photos/{photo.Id}", admin);
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        Assert.Equal(FileDeletionState.Completed, (await ReadAsync<PhotoRemovedDto>(removed)).FileDeletion);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/v1/review-photos/{photo.Id}")).StatusCode);
        Assert.Empty((await ReadAsync<ReviewDetailDto>(await _api.SendAsync(HttpMethod.Get, $"/api/v1/reviews/{reviewId}", author))).Photos);
    }

    [Fact]
    public async Task Uploads_AreRefusedForBadFilesAndOtherUsers_AndNeverRedeemTheToken()
    {
        var token = await IssueTokenAsync();
        var author = await _api.RegisterAsync();
        var other = await _api.RegisterAsync();

        var fake = await UploadAsync(author, token, Guid.NewGuid(), "not an image"u8.ToArray());
        var large = await UploadAsync(author, token, Guid.NewGuid(), new byte[(5 * 1024 * 1024) + 1]);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, fake.StatusCode);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, large.StatusCode);

        Assert.Equal(HttpStatusCode.Created, (await UploadAsync(author, token, Guid.NewGuid(), Jpeg())).StatusCode);
        var claimed = await UploadAsync(other, token, Guid.NewGuid(), Jpeg());
        var otherReview = await _api.SendAsync(HttpMethod.Post, "/api/v1/verification/reviews", other, new
        {
            token, rating = 5, foodRating = 5, serviceRating = 5, valueRating = 5, atmosphereRating = 5, text = "x",
        });
        Assert.Equal(HttpStatusCode.Forbidden, claimed.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, otherReview.StatusCode);
        Assert.Contains("token_claimed", await otherReview.Content.ReadAsStringAsync());

        // The token is still usable by its claimant.
        var resolve = await _api.SendAsync(HttpMethod.Post, "/api/v1/verification/visits/resolve", author, new { token });
        Assert.Equal(HttpStatusCode.OK, resolve.StatusCode);
    }

    [Fact]
    public async Task UploaderDeletesTheirOwnUnattachedPhoto_ButNotSomeoneElses()
    {
        var token = await IssueTokenAsync();
        var author = await _api.RegisterAsync();
        var other = await _api.RegisterAsync();
        var photo = await ReadAsync<UploadedPhotoDto>(await UploadAsync(author, token, Guid.NewGuid(), Jpeg()));

        var byOther = await _api.SendAsync(HttpMethod.Delete, $"/api/v1/verification/photos/{photo.Id}", other);
        var byAuthor = await _api.SendAsync(HttpMethod.Delete, $"/api/v1/verification/photos/{photo.Id}", author);

        Assert.Equal(HttpStatusCode.NotFound, byOther.StatusCode);
        Assert.Equal(HttpStatusCode.OK, byAuthor.StatusCode);
        Assert.True((await ReadAsync<PhotoRemovedDto>(byAuthor)).Removed);
    }

    [Fact]
    public async Task FeedAndRestaurantReviews_IncludePhotos()
    {
        var token = await IssueTokenAsync();
        var author = await _api.RegisterUserAsync();
        var follower = await _api.RegisterAsync();
        var photo = await ReadAsync<UploadedPhotoDto>(await UploadAsync(author.AccessToken, token, Guid.NewGuid(), Jpeg()));
        var review = await _api.SendAsync(HttpMethod.Post, "/api/v1/verification/reviews", author.AccessToken, new
        {
            token, rating = 4, foodRating = 4, serviceRating = 4, valueRating = 4, atmosphereRating = 4, text = "x",
            photoIds = new[] { photo.Id },
        });
        var restaurantId = (await ReadAsync<VerifiedReviewCreatedDto>(review)).RestaurantId;
        await _api.SendAsync(HttpMethod.Put, $"/api/v1/users/{author.User.Id}/follow", follower);

        var feed = await ReadAsync<FeedResultDto>(await _api.SendAsync(HttpMethod.Get, "/api/v1/feed", follower));
        var list = await ReadAsync<ReviewListResultDto>(
            await _api.SendAsync(HttpMethod.Get, $"/api/v1/restaurants/{restaurantId}/reviews", follower));

        Assert.Equal(photo.Id, Assert.Single(Assert.Single(feed.Items).Photos).Id);
        Assert.Equal(photo.Id, Assert.Single(Assert.Single(list.Items).Photos).Id);
    }

    private Task<HttpResponseMessage> UploadAsync(string accessToken, string token, Guid uploadId, byte[] file)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/verification/photos") { Content = Form(token, uploadId, file) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return _client.SendAsync(request);
    }

    private static MultipartFormDataContent Form(string token, Guid uploadId, byte[] file)
    {
        var content = new ByteArrayContent(file);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        return new MultipartFormDataContent
        {
            { new StringContent(token), "token" },
            { new StringContent(uploadId.ToString()), "clientUploadId" },
            { content, "file", "photo.jpg" },
        };
    }

    private async Task<string> IssueTokenAsync()
    {
        var restaurantId = Guid.NewGuid();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.Restaurants.Add(new Restaurant
            {
                Id = restaurantId,
                Name = $"photos-{Guid.NewGuid():N}",
                Description = "وصف",
                City = "Amman",
                Latitude = 31.95,
                Longitude = 35.91,
                PriceLevel = PriceLevel.Moderate,
                IsActive = true,
                IsPartner = true,
                CreatedAt = DateTime.UtcNow,
            });
            await context.SaveChangesAsync();
        }

        var admin = await _api.AdminTokenAsync();
        var issued = await _api.SendAsync(HttpMethod.Post, $"/api/v1/admin/restaurants/{restaurantId}/visit-tokens", admin, new { count = 1 });
        return (await ReadAsync<IssuedVisitTokensDto>(issued)).Payloads.Single();
    }

    private static byte[] Jpeg()
    {
        using var bitmap = new SKBitmap(32, 24);
        bitmap.Erase(SKColors.SeaGreen);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
        return data.ToArray();
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ApiResponse<T>>(JsonOptions))!.Data!;
}
