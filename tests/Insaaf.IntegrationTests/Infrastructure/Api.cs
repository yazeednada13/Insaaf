using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Insaaf.Domain.Constants;
using Insaaf.Infrastructure.Identity;
using Insaaf.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;

namespace Insaaf.IntegrationTests.Infrastructure;

/// <summary>A parsed API response: status, the <c>ApiResponse</c> envelope and the raw text.</summary>
public sealed record ApiResult(HttpStatusCode Status, JsonElement Body, string Raw, HttpResponseMessage Response)
{
    public JsonElement Data => Body.GetProperty("data");

    public string? ErrorCode =>
        Body.ValueKind == JsonValueKind.Object
        && Body.TryGetProperty("errors", out var errors)
        && errors.ValueKind == JsonValueKind.Array
        && errors.GetArrayLength() > 0
            ? errors[0].GetProperty("code").GetString()
            : null;
}

public sealed record TestUser(Guid Id, string Email, string AccessToken, string RefreshToken);

/// <summary>Drives the real API over HTTP, the way the smoke scripts did, with run-tagged test data.</summary>
public sealed class Api
{
    public const string Password = "Password1!";

    private readonly SqlServerApiFactory _factory;

    public Api(SqlServerApiFactory factory)
    {
        _factory = factory;
        Client = factory.CreateClient();
    }

    public HttpClient Client { get; }

    public async Task<ApiResult> SendAsync(HttpMethod method, string path, string? token = null, object? body = null)
    {
        using var request = new HttpRequestMessage(method, "/api/v1" + path);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (body is not null)
        {
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        }

        return await ParseAsync(await Client.SendAsync(request));
    }

    public static async Task<ApiResult> ParseAsync(HttpResponseMessage response)
    {
        var raw = await response.Content.ReadAsStringAsync();
        var body = default(JsonElement);
        if (raw.Length > 0 && response.Content.Headers.ContentType?.MediaType?.Contains("json") == true)
        {
            body = JsonDocument.Parse(raw).RootElement.Clone();
        }

        return new ApiResult(response.StatusCode, body, raw, response);
    }

    public async Task<TestUser> RegisterAsync()
    {
        var id = Guid.NewGuid().ToString("N");
        var email = $"{_factory.EmailPrefix}{id}@example.com";
        var result = await SendAsync(HttpMethod.Post, "/auth/register", null, new
        {
            email, password = Password, fullName = "مستخدم اختبار", username = $"it_{id[..20]}",
        });
        Assert.True(result.Status is HttpStatusCode.OK or HttpStatusCode.Created, $"register: {result.Status} {result.Raw}");
        return ToUser(result, email);
    }

    public async Task<ApiResult> LoginAsync(string email) =>
        await SendAsync(HttpMethod.Post, "/auth/login", null, new { email, password = Password });

    /// <summary>A new account granted Admin, then signed in again (roles are embedded at sign-in).</summary>
    public async Task<TestUser> AdminAsync()
    {
        var user = await RegisterAsync();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var account = await users.FindByIdAsync(user.Id.ToString());
            await users.AddToRoleAsync(account!, Roles.Admin);
        }

        var login = await LoginAsync(user.Email);
        Assert.Equal(HttpStatusCode.OK, login.Status);
        return ToUser(login, user.Email);
    }

    /// <summary>A run-tagged partner restaurant created through the Admin API.</summary>
    public async Task<Guid> CreatePartnerRestaurantAsync(TestUser admin)
    {
        string slug;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            slug = await context.Categories.AsNoTracking().OrderBy(category => category.Slug).Select(category => category.Slug).FirstAsync();
        }

        var created = await SendAsync(HttpMethod.Post, "/admin/restaurants", admin.AccessToken, new
        {
            name = $"{_factory.RestaurantPrefix} {Guid.NewGuid():N}"[..40],
            description = "مطعم اختبار التكامل",
            city = "Amman",
            address = (string?)null,
            latitude = 31.95,
            longitude = 35.91,
            priceLevel = 2,
            phone = (string?)null,
            imageUrl = (string?)null,
            timeZoneId = (string?)null,
            categorySlugs = new[] { slug },
            openingHours = Array.Empty<object>(),
        });
        Assert.True(created.Status == HttpStatusCode.Created, $"create restaurant: {created.Status} {created.Raw}");
        var id = created.Data.GetProperty("id").GetGuid();

        var partner = await SendAsync(HttpMethod.Put, $"/admin/restaurants/{id}/partnership", admin.AccessToken, new { isPartner = true });
        Assert.Equal(HttpStatusCode.OK, partner.Status);
        return id;
    }

    public async Task<string[]> IssueTokensAsync(TestUser admin, Guid restaurantId, int count)
    {
        var issued = await SendAsync(HttpMethod.Post, $"/admin/restaurants/{restaurantId}/visit-tokens", admin.AccessToken, new { count });
        Assert.True(issued.Status == HttpStatusCode.Created, $"issue tokens: {issued.Status} {issued.Raw}");
        return issued.Data.GetProperty("payloads").EnumerateArray().Select(payload => payload.GetString()!).ToArray();
    }

    public Task<ApiResult> CreateReviewAsync(TestUser author, string token, int rating, Guid[]? photoIds = null) =>
        SendAsync(HttpMethod.Post, "/verification/reviews", author.AccessToken, new
        {
            token, rating, foodRating = rating, serviceRating = rating, valueRating = rating, atmosphereRating = rating,
            text = "تقييم اختبار التكامل", photoIds = photoIds ?? [],
        });

    /// <summary>A partner restaurant with one verified review created through the QR flow.</summary>
    public async Task<(Guid RestaurantId, Guid ReviewId, TestUser Author, TestUser Admin)> CreateVerifiedReviewAsync(int rating = 4)
    {
        var admin = await AdminAsync();
        var restaurantId = await CreatePartnerRestaurantAsync(admin);
        var token = (await IssueTokensAsync(admin, restaurantId, 1)).Single();
        var author = await RegisterAsync();
        var review = await CreateReviewAsync(author, token, rating);
        Assert.True(review.Status == HttpStatusCode.Created, $"create review: {review.Status} {review.Raw}");
        return (restaurantId, review.Data.GetProperty("reviewId").GetGuid(), author, admin);
    }

    public async Task<ApiResult> UploadPhotoAsync(TestUser user, string token, Guid clientUploadId, byte[] bytes)
    {
        using var form = new MultipartFormDataContent
        {
            { new StringContent(token), "token" },
            { new StringContent(clientUploadId.ToString()), "clientUploadId" },
        };
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(file, "file", "photo.jpg");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/verification/photos") { Content = form };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user.AccessToken);
        return await ParseAsync(await Client.SendAsync(request));
    }

    public static byte[] Jpeg(SKColor color)
    {
        using var bitmap = new SKBitmap(64, 48);
        bitmap.Erase(color);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
        return data.ToArray();
    }

    private static TestUser ToUser(ApiResult result, string email)
    {
        var data = result.Data;
        return new TestUser(
            data.GetProperty("user").GetProperty("id").GetGuid(),
            email,
            data.GetProperty("accessToken").GetString()!,
            data.GetProperty("refreshToken").GetString()!);
    }
}
