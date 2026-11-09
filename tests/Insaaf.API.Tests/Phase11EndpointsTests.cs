using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Insaaf.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Insaaf.API.Tests;

/// <summary>Phase 11 (Completion): saved items, comment likes, reports, official replies, home and photos.</summary>
public sealed class Phase11EndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly SocialApi _api;

    public Phase11EndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _api = new SocialApi(factory, factory.CreateClient());
    }

    // ── 11.2 Saved ──────────────────────────────────────────────────────────

    [Fact]
    public async Task SavingAReview_IsRepeatSafe_PrivateAndShownOnReads()
    {
        var (restaurantId, reviewId, author) = await _api.CreateVerifiedReviewAsync();
        var other = await _api.RegisterAsync();

        var first = await JsonAsync(await _api.SendAsync(HttpMethod.Put, $"/api/v1/reviews/{reviewId}/save", author));
        var repeat = await JsonAsync(await _api.SendAsync(HttpMethod.Put, $"/api/v1/reviews/{reviewId}/save", author));
        Assert.True(first.GetProperty("isSavedByMe").GetBoolean());
        Assert.True(repeat.GetProperty("isSavedByMe").GetBoolean());

        var detail = await JsonAsync(await _api.SendAsync(HttpMethod.Get, $"/api/v1/reviews/{reviewId}", author));
        var otherDetail = await JsonAsync(await _api.SendAsync(HttpMethod.Get, $"/api/v1/reviews/{reviewId}", other));
        var list = await JsonAsync(await _api.SendAsync(HttpMethod.Get, $"/api/v1/restaurants/{restaurantId}/reviews", author));
        Assert.True(detail.GetProperty("isSavedByMe").GetBoolean());
        Assert.False(otherDetail.GetProperty("isSavedByMe").GetBoolean());
        Assert.True(list.GetProperty("items")[0].GetProperty("isSavedByMe").GetBoolean());

        var mine = await JsonAsync(await _api.SendAsync(HttpMethod.Get, "/api/v1/users/me/saved/reviews", author));
        var theirs = await JsonAsync(await _api.SendAsync(HttpMethod.Get, "/api/v1/users/me/saved/reviews", other));
        Assert.Equal(reviewId, Assert.Single(mine.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());
        Assert.Empty(theirs.GetProperty("items").EnumerateArray());

        var unsave = await JsonAsync(await _api.SendAsync(HttpMethod.Delete, $"/api/v1/reviews/{reviewId}/save", author));
        Assert.False(unsave.GetProperty("isSavedByMe").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Delete, $"/api/v1/reviews/{reviewId}/save", author)).StatusCode);
        var empty = await JsonAsync(await _api.SendAsync(HttpMethod.Get, "/api/v1/users/me/saved/reviews", author));
        Assert.Empty(empty.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task SavingARestaurant_ShowsOnItsDetailAndInTheSavedList()
    {
        var (restaurantId, _, user) = await _api.CreateVerifiedReviewAsync();

        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Put, $"/api/v1/restaurants/{restaurantId}/save", user)).StatusCode);
        var detail = await JsonAsync(await _api.SendAsync(HttpMethod.Get, $"/api/v1/restaurants/{restaurantId}", user));
        Assert.True(detail.GetProperty("isSavedByMe").GetBoolean());

        var saved = await JsonAsync(await _api.SendAsync(HttpMethod.Get, "/api/v1/users/me/saved/restaurants", user));
        Assert.Equal(1, saved.GetProperty("totalCount").GetInt32());
        Assert.Equal(restaurantId, saved.GetProperty("items")[0].GetProperty("id").GetGuid());

        // A deactivated restaurant leaves the list but can still be unsaved.
        await SetRestaurantActiveAsync(restaurantId, false);
        var afterDeactivation = await JsonAsync(await _api.SendAsync(HttpMethod.Get, "/api/v1/users/me/saved/restaurants", user));
        Assert.Equal(0, afterDeactivation.GetProperty("totalCount").GetInt32());
        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Delete, $"/api/v1/restaurants/{restaurantId}/save", user)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _api.SendAsync(HttpMethod.Put, $"/api/v1/restaurants/{restaurantId}/save", user)).StatusCode);
    }

    [Fact]
    public async Task SavingUnknownItems_IsNotFound_AndBadPagingIsRefused()
    {
        var user = await _api.RegisterAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await _api.SendAsync(HttpMethod.Put, $"/api/v1/reviews/{Guid.NewGuid()}/save", user)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _api.SendAsync(HttpMethod.Put, $"/api/v1/restaurants/{Guid.NewGuid()}/save", user)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _api.SendAsync(HttpMethod.Get, "/api/v1/users/me/saved/restaurants?pageSize=51", user)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _api.SendAsync(HttpMethod.Get, "/api/v1/users/me/saved/reviews?cursor=nope", user)).StatusCode);
    }

    // ── 11.3 Comment likes ──────────────────────────────────────────────────

    [Fact]
    public async Task CommentLikes_AreRepeatSafe_PerUser_AndScopedToTheirReview()
    {
        var (_, reviewId, author) = await _api.CreateVerifiedReviewAsync();
        var (_, otherReviewId, _) = await _api.CreateVerifiedReviewAsync();
        var liker = await _api.RegisterAsync();
        var comment = await JsonAsync(await _api.SendAsync(HttpMethod.Post, $"/api/v1/reviews/{reviewId}/comments", author, new { text = "تعليق" }));
        var commentId = comment.GetProperty("id").GetGuid();

        var like = await JsonAsync(await _api.SendAsync(HttpMethod.Put, $"/api/v1/reviews/{reviewId}/comments/{commentId}/like", liker));
        var again = await JsonAsync(await _api.SendAsync(HttpMethod.Put, $"/api/v1/reviews/{reviewId}/comments/{commentId}/like", liker));
        Assert.Equal(1, like.GetProperty("likeCount").GetInt32());
        Assert.Equal(1, again.GetProperty("likeCount").GetInt32());

        var asLiker = await JsonAsync(await _api.SendAsync(HttpMethod.Get, $"/api/v1/reviews/{reviewId}/comments", liker));
        var asAuthor = await JsonAsync(await _api.SendAsync(HttpMethod.Get, $"/api/v1/reviews/{reviewId}/comments", author));
        Assert.True(asLiker.GetProperty("items")[0].GetProperty("isLikedByMe").GetBoolean());
        Assert.False(asAuthor.GetProperty("items")[0].GetProperty("isLikedByMe").GetBoolean());
        Assert.Equal(1, asAuthor.GetProperty("items")[0].GetProperty("likeCount").GetInt32());

        // The comment must belong to the review in the route.
        Assert.Equal(HttpStatusCode.NotFound, (await _api.SendAsync(HttpMethod.Put, $"/api/v1/reviews/{otherReviewId}/comments/{commentId}/like", liker)).StatusCode);

        var unlike = await JsonAsync(await _api.SendAsync(HttpMethod.Delete, $"/api/v1/reviews/{reviewId}/comments/{commentId}/like", liker));
        Assert.Equal(0, unlike.GetProperty("likeCount").GetInt32());
        Assert.False(unlike.GetProperty("isLikedByMe").GetBoolean());

        // Deleting the comment removes its likes with it.
        await _api.SendAsync(HttpMethod.Put, $"/api/v1/reviews/{reviewId}/comments/{commentId}/like", liker);
        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Delete, $"/api/v1/reviews/{reviewId}/comments/{commentId}", author)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _api.SendAsync(HttpMethod.Put, $"/api/v1/reviews/{reviewId}/comments/{commentId}/like", liker)).StatusCode);
    }

    // ── 11.4 Reports ────────────────────────────────────────────────────────

    [Fact]
    public async Task Reports_AreValidated_OneOpenPerUser_AndAdminResolvesThem()
    {
        var (_, reviewId, _) = await _api.CreateVerifiedReviewAsync();
        var reporter = await _api.RegisterAsync();
        var admin = await _api.AdminTokenAsync();

        var badReason = await _api.SendAsync(HttpMethod.Post, $"/api/v1/reviews/{reviewId}/report", reporter, new { reason = "spam" });
        var extraField = await _api.SendAsync(HttpMethod.Post, $"/api/v1/reviews/{reviewId}/report", reporter, new { reason = "other", userId = Guid.NewGuid() });
        var unknown = await _api.SendAsync(HttpMethod.Post, $"/api/v1/reviews/{Guid.NewGuid()}/report", reporter, new { reason = "other" });
        Assert.Equal(HttpStatusCode.BadRequest, badReason.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, extraField.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);

        var first = await JsonAsync(await _api.SendAsync(HttpMethod.Post, $"/api/v1/reviews/{reviewId}/report", reporter, new { reason = "not_genuine", note = "  ملاحظة  " }));
        await JsonAsync(await _api.SendAsync(HttpMethod.Post, $"/api/v1/reviews/{reviewId}/report", reporter, new { reason = "offensive" }));
        Assert.True(first.GetProperty("reported").GetBoolean());

        // Users can't see the queue; Admin sees exactly one open report from this reporter.
        Assert.Equal(HttpStatusCode.Forbidden, (await _api.SendAsync(HttpMethod.Get, "/api/v1/admin/reports", reporter)).StatusCode);
        var queue = await JsonAsync(await _api.SendAsync(HttpMethod.Get, "/api/v1/admin/reports?limit=50", admin));
        var mine = queue.GetProperty("items").EnumerateArray().Where(item => item.GetProperty("reviewId").GetGuid() == reviewId).ToList();
        var report = Assert.Single(mine);
        Assert.Equal("not_genuine", report.GetProperty("reason").GetString());
        Assert.Equal("ملاحظة", report.GetProperty("note").GetString());
        Assert.False(report.GetProperty("isReviewHidden").GetBoolean());

        var reportId = report.GetProperty("id").GetGuid();
        var resolved = await JsonAsync(await _api.SendAsync(HttpMethod.Put, $"/api/v1/admin/reports/{reportId}/resolve", admin));
        Assert.Equal(JsonValueKind.String, resolved.GetProperty("resolvedAt").ValueKind);
        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Put, $"/api/v1/admin/reports/{reportId}/resolve", admin)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _api.SendAsync(HttpMethod.Put, $"/api/v1/admin/reports/{Guid.NewGuid()}/resolve", admin)).StatusCode);

        // Resolving never touches the review, and the user can report it again afterwards.
        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Get, $"/api/v1/reviews/{reviewId}", reporter)).StatusCode);
        await JsonAsync(await _api.SendAsync(HttpMethod.Post, $"/api/v1/reviews/{reviewId}/report", reporter, new { reason = "other" }));
        var all = await JsonAsync(await _api.SendAsync(HttpMethod.Get, "/api/v1/admin/reports?status=all&limit=50", admin));
        Assert.Equal(2, all.GetProperty("items").EnumerateArray().Count(item => item.GetProperty("reviewId").GetGuid() == reviewId));
        Assert.Equal(HttpStatusCode.BadRequest, (await _api.SendAsync(HttpMethod.Get, "/api/v1/admin/reports?status=x", admin)).StatusCode);
    }

    // ── 11.5 Official reply ─────────────────────────────────────────────────

    [Fact]
    public async Task OfficialReply_ShowsOnReads_NotifiesOnce_AndNeverChangesRatings()
    {
        var (restaurantId, reviewId, author) = await _api.CreateVerifiedReviewAsync();
        var admin = await _api.AdminTokenAsync();
        var before = await JsonAsync(await _api.SendAsync(HttpMethod.Get, $"/api/v1/restaurants/{restaurantId}", author));

        Assert.Equal(HttpStatusCode.Forbidden, (await _api.SendAsync(HttpMethod.Put, $"/api/v1/admin/reviews/{reviewId}/reply", author, new { text = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _api.SendAsync(HttpMethod.Put, $"/api/v1/admin/reviews/{reviewId}/reply", admin, new { text = "   " })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _api.SendAsync(HttpMethod.Put, $"/api/v1/admin/reviews/{Guid.NewGuid()}/reply", admin, new { text = "شكرًا" })).StatusCode);

        await JsonAsync(await _api.SendAsync(HttpMethod.Put, $"/api/v1/admin/reviews/{reviewId}/reply", admin, new { text = " شكرًا لزيارتك " }));
        var replaced = await JsonAsync(await _api.SendAsync(HttpMethod.Put, $"/api/v1/admin/reviews/{reviewId}/reply", admin, new { text = "شكرًا جزيلًا" }));
        Assert.Equal("شكرًا جزيلًا", replaced.GetProperty("text").GetString());

        var detail = await JsonAsync(await _api.SendAsync(HttpMethod.Get, $"/api/v1/reviews/{reviewId}", author));
        var list = await JsonAsync(await _api.SendAsync(HttpMethod.Get, $"/api/v1/restaurants/{restaurantId}/reviews", author));
        Assert.Equal("شكرًا جزيلًا", detail.GetProperty("restaurantReply").GetProperty("text").GetString());
        Assert.Equal("شكرًا جزيلًا", list.GetProperty("items")[0].GetProperty("restaurantReply").GetProperty("text").GetString());

        // One reply notification, shown with the restaurant, not the Admin.
        var notifications = await JsonAsync(await _api.SendAsync(HttpMethod.Get, "/api/v1/notifications", author));
        var reply = Assert.Single(notifications.GetProperty("items").EnumerateArray(), item => item.GetProperty("type").GetString() == "reply");
        Assert.Equal(restaurantId, reply.GetProperty("restaurant").GetProperty("id").GetGuid());
        Assert.False(reply.GetProperty("actor").GetProperty("hasPublicProfile").GetBoolean());

        var after = await JsonAsync(await _api.SendAsync(HttpMethod.Get, $"/api/v1/restaurants/{restaurantId}", author));
        Assert.Equal(before.GetProperty("averageRating").GetDecimal(), after.GetProperty("averageRating").GetDecimal());
        Assert.Equal(before.GetProperty("reviewCount").GetInt32(), after.GetProperty("reviewCount").GetInt32());

        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Delete, $"/api/v1/admin/reviews/{reviewId}/reply", admin)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _api.SendAsync(HttpMethod.Delete, $"/api/v1/admin/reviews/{reviewId}/reply", admin)).StatusCode);
        var cleared = await JsonAsync(await _api.SendAsync(HttpMethod.Get, $"/api/v1/reviews/{reviewId}", author));
        Assert.Equal(JsonValueKind.Null, cleared.GetProperty("restaurantReply").ValueKind);
        var noNotification = await JsonAsync(await _api.SendAsync(HttpMethod.Get, "/api/v1/notifications", author));
        Assert.DoesNotContain(noNotification.GetProperty("items").EnumerateArray(), item => item.GetProperty("type").GetString() == "reply");
    }

    // ── 11.6 Recent reviews and community photos ────────────────────────────

    [Fact]
    public async Task RecentReviews_ListEveryonesPublicReviews_NewestFirst_AndPage()
    {
        var (_, older, _) = await _api.CreateVerifiedReviewAsync();
        var (_, newer, _) = await _api.CreateVerifiedReviewAsync();
        var viewer = await _api.RegisterAsync();

        var page = await JsonAsync(await _api.SendAsync(HttpMethod.Get, "/api/v1/home/recent?limit=50", viewer));
        var ids = page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToList();
        Assert.True(ids.IndexOf(newer) >= 0 && ids.IndexOf(newer) < ids.IndexOf(older), "newest first");

        var first = await JsonAsync(await _api.SendAsync(HttpMethod.Get, "/api/v1/home/recent?limit=1", viewer));
        var cursor = first.GetProperty("nextCursor").GetString();
        var second = await JsonAsync(await _api.SendAsync(HttpMethod.Get, $"/api/v1/home/recent?limit=1&cursor={Uri.EscapeDataString(cursor!)}", viewer));
        Assert.NotEqual(first.GetProperty("items")[0].GetProperty("id").GetGuid(), second.GetProperty("items")[0].GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.BadRequest, (await _api.SendAsync(HttpMethod.Get, "/api/v1/home/recent?limit=51", viewer)).StatusCode);
    }

    [Fact]
    public async Task CommunityPhotos_ListTheRestaurantsReviewPhotos_AndFollowModeration()
    {
        var restaurantId = await InsertPartnerRestaurantAsync();
        var admin = await _api.AdminTokenAsync();
        var issued = await JsonAsync(await _api.SendAsync(HttpMethod.Post, $"/api/v1/admin/restaurants/{restaurantId}/visit-tokens", admin, new { count = 1 }));
        var token = issued.GetProperty("payloads")[0].GetString()!;
        var author = await _api.RegisterAsync();
        var photoId = await UploadAsync(author, token);
        var review = await JsonAsync(await _api.SendAsync(HttpMethod.Post, "/api/v1/verification/reviews", author, new
        {
            token, rating = 5, foodRating = 5, serviceRating = 5, valueRating = 5, atmosphereRating = 5, text = "صور", photoIds = new[] { photoId },
        }));
        var reviewId = review.GetProperty("reviewId").GetGuid();

        var photos = await JsonAsync(await _api.SendAsync(HttpMethod.Get, $"/api/v1/restaurants/{restaurantId}/photos", author));
        var item = Assert.Single(photos.GetProperty("items").EnumerateArray());
        Assert.Equal(photoId, item.GetProperty("id").GetGuid());
        Assert.Equal(reviewId, item.GetProperty("reviewId").GetGuid());
        Assert.Equal($"/api/v1/review-photos/{photoId}?size=thumb", item.GetProperty("thumbnailUrl").GetString());

        // A hidden review takes its photos out of the list.
        await _api.SendAsync(HttpMethod.Put, $"/api/v1/admin/reviews/{reviewId}/visibility", admin, new { hidden = true });
        var hidden = await JsonAsync(await _api.SendAsync(HttpMethod.Get, $"/api/v1/restaurants/{restaurantId}/photos", author));
        Assert.Empty(hidden.GetProperty("items").EnumerateArray());

        Assert.Equal(HttpStatusCode.NotFound, (await _api.SendAsync(HttpMethod.Get, $"/api/v1/restaurants/{Guid.NewGuid()}/photos", author)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _api.SendAsync(HttpMethod.Get, $"/api/v1/restaurants/{restaurantId}/photos?cursor=nope", author)).StatusCode);
    }

    // ── 11.7 Avatars ────────────────────────────────────────────────────────

    [Fact]
    public async Task Avatar_UploadReplaceAndRemove_ShowsEverywhere_AndKeepsOneFile()
    {
        var (_, reviewId, author) = await _api.CreateVerifiedReviewAsync();
        var client = _factory.CreateClient();

        var bad = await UploadAvatarAsync(author, "not an image"u8.ToArray());
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, bad.StatusCode);

        var first = await JsonAsync(await UploadAvatarAsync(author, Jpeg(300, 200)));
        var firstUrl = first.GetProperty("avatarUrl").GetString()!;
        var me = await JsonAsync(await _api.SendAsync(HttpMethod.Get, "/api/v1/users/me", author));
        Assert.Equal(firstUrl, me.GetProperty("avatarUrl").GetString());
        var detail = await JsonAsync(await _api.SendAsync(HttpMethod.Get, $"/api/v1/reviews/{reviewId}", author));
        Assert.Equal(firstUrl, detail.GetProperty("reviewer").GetProperty("avatarUrl").GetString());

        // Served anonymously as a centred square, with revalidation headers.
        var image = await client.GetAsync(firstUrl);
        Assert.Equal(HttpStatusCode.OK, image.StatusCode);
        Assert.True(image.Headers.CacheControl!.NoCache);
        using (var bitmap = SkiaSharp.SKBitmap.Decode(await image.Content.ReadAsByteArrayAsync()))
        {
            Assert.Equal((200, 200), (bitmap.Width, bitmap.Height));
        }

        var second = await JsonAsync(await UploadAvatarAsync(author, Jpeg(64, 64)));
        var secondUrl = second.GetProperty("avatarUrl").GetString()!;
        Assert.NotEqual(firstUrl, secondUrl);
        // Replacing deletes the previous file; only the new one remains for this user.
        Assert.False(File.Exists(AvatarFile(firstUrl)));
        Assert.True(File.Exists(AvatarFile(secondUrl)));

        var removed = await JsonAsync(await _api.SendAsync(HttpMethod.Delete, "/api/v1/users/me/avatar", author));
        Assert.Equal(JsonValueKind.Null, removed.GetProperty("avatarUrl").ValueKind);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(firstUrl)).StatusCode);
        Assert.False(File.Exists(AvatarFile(secondUrl)));
    }

    [Fact]
    public async Task AdminAvatar_IsNeverServed()
    {
        var admin = await _api.AdminTokenAsync();
        var uploaded = await JsonAsync(await UploadAvatarAsync(admin, Jpeg(80, 80)));

        var response = await _factory.CreateClient().GetAsync(uploaded.GetProperty("avatarUrl").GetString());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── 11.8 Taste Setup ────────────────────────────────────────────────────

    [Fact]
    public async Task TasteSetup_IsValidated_Saved_AndReportedOnMe()
    {
        var user = await _api.RegisterAsync();
        var slug = await AnyCategorySlugAsync();

        var before = await JsonAsync(await _api.SendAsync(HttpMethod.Get, "/api/v1/users/me", user));
        Assert.False(before.GetProperty("hasCompletedTasteSetup").GetBoolean());

        Assert.Equal(HttpStatusCode.BadRequest, (await _api.SendAsync(HttpMethod.Put, "/api/v1/users/me/taste-preferences", user, new { categorySlugs = Array.Empty<string>(), priorities = Array.Empty<string>() })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _api.SendAsync(HttpMethod.Put, "/api/v1/users/me/taste-preferences", user, new { categorySlugs = new[] { "steak" }, priorities = Array.Empty<string>() })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _api.SendAsync(HttpMethod.Put, "/api/v1/users/me/taste-preferences", user, new { categorySlugs = new[] { slug }, priorities = new[] { "speed" } })).StatusCode);

        var saved = await JsonAsync(await _api.SendAsync(HttpMethod.Put, "/api/v1/users/me/taste-preferences", user, new { categorySlugs = new[] { slug, slug.ToUpperInvariant() }, priorities = new[] { "service", "price" } }));
        Assert.Equal(new[] { slug }, saved.GetProperty("categorySlugs").EnumerateArray().Select(item => item.GetString()!).ToArray());
        Assert.Equal(new[] { "service", "price" }, saved.GetProperty("priorities").EnumerateArray().Select(item => item.GetString()!).ToArray());
        Assert.True(saved.GetProperty("hasCompletedTasteSetup").GetBoolean());

        var after = await JsonAsync(await _api.SendAsync(HttpMethod.Get, "/api/v1/users/me", user));
        Assert.True(after.GetProperty("hasCompletedTasteSetup").GetBoolean());
        // Other people's profiles don't carry the flag.
        var other = await _api.RegisterUserAsync();
        var otherProfile = await JsonAsync(await _api.SendAsync(HttpMethod.Get, $"/api/v1/users/{other.User.Id}", user));
        Assert.Equal(JsonValueKind.Null, otherProfile.GetProperty("hasCompletedTasteSetup").ValueKind);
    }

    [Fact]
    public async Task SuggestedUsers_HaveReviews_AndExcludeSelfFollowedAndAdmin()
    {
        var (_, _, author) = await _api.CreateVerifiedReviewAsync();
        var authorId = (await JsonAsync(await _api.SendAsync(HttpMethod.Get, "/api/v1/users/me", author))).GetProperty("id").GetGuid();
        var viewer = await _api.RegisterAsync();

        var suggested = await JsonAsync(await _api.SendAsync(HttpMethod.Get, "/api/v1/users/suggested?limit=20", viewer));
        var ids = suggested.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToList();
        Assert.Contains(authorId, ids);
        Assert.All(suggested.GetProperty("items").EnumerateArray(), item => Assert.True(item.GetProperty("reviewCount").GetInt32() > 0));

        await _api.SendAsync(HttpMethod.Put, $"/api/v1/users/{authorId}/follow", viewer);
        var afterFollow = await JsonAsync(await _api.SendAsync(HttpMethod.Get, "/api/v1/users/suggested?limit=20", viewer));
        Assert.DoesNotContain(authorId, afterFollow.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()));

        var asAuthor = await JsonAsync(await _api.SendAsync(HttpMethod.Get, "/api/v1/users/suggested?limit=20", author));
        Assert.DoesNotContain(authorId, asAuthor.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()));
        Assert.Equal(HttpStatusCode.BadRequest, (await _api.SendAsync(HttpMethod.Get, "/api/v1/users/suggested?limit=21", viewer)).StatusCode);
    }

    // ── 11.9 Passwords ──────────────────────────────────────────────────────

    [Fact]
    public async Task PasswordReset_WithTheEmailedCode_SignsOutEverywhere_AndTheCodeIsSingleUse()
    {
        var registered = await _api.RegisterUserAsync();
        var client = _factory.CreateClient();
        var email = registered.User.Email;

        var unknown = await JsonAsync(await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = $"nobody-{Guid.NewGuid():N}@example.com" }));
        var known = await JsonAsync(await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email }));
        Assert.Equal(unknown.GetRawText(), known.GetRawText());
        var code = LatestCodeFor(email);

        var wrong = await client.PostAsJsonAsync("/api/v1/auth/reset-password", new { email, code = code == "000000" ? "111111" : "000000", newPassword = "NewPassword1!" });
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.Contains("invalid_code", await wrong.Content.ReadAsStringAsync());

        await JsonAsync(await client.PostAsJsonAsync("/api/v1/auth/reset-password", new { email, code, newPassword = "NewPassword1!" }));
        var reused = await client.PostAsJsonAsync("/api/v1/auth/reset-password", new { email, code, newPassword = "Another1!pass" });
        Assert.Equal(HttpStatusCode.BadRequest, reused.StatusCode);

        // Every refresh token is revoked; only the new password signs in.
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = registered.RefreshToken })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Password1!" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "NewPassword1!" })).StatusCode);
    }

    [Fact]
    public async Task ChangePassword_NeedsTheCurrentOne_AndReturnsFreshTokens()
    {
        var registered = await _api.RegisterUserAsync();
        var client = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/change-password", new { currentPassword = "Password1!", newPassword = "Changed1!pass" })).StatusCode);
        var wrong = await _api.SendAsync(HttpMethod.Post, "/api/v1/auth/change-password", registered.AccessToken, new { currentPassword = "Nope1!nope", newPassword = "Changed1!pass" });
        Assert.Contains("wrong_password", await wrong.Content.ReadAsStringAsync());

        var changed = await JsonAsync(await _api.SendAsync(HttpMethod.Post, "/api/v1/auth/change-password", registered.AccessToken, new { currentPassword = "Password1!", newPassword = "Changed1!pass" }));
        var newRefresh = changed.GetProperty("refreshToken").GetString();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = registered.RefreshToken })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = newRefresh })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/login", new { email = registered.User.Email, password = "Changed1!pass" })).StatusCode);
    }

    // ── 11.10 Notification settings ─────────────────────────────────────────

    [Fact]
    public async Task NotificationSettings_DefaultOn_AndATurnedOffTypeCreatesNothing()
    {
        var a = await _api.RegisterUserAsync();
        var b = await _api.RegisterAsync();

        var defaults = await JsonAsync(await _api.SendAsync(HttpMethod.Get, "/api/v1/users/me/settings", a.AccessToken));
        Assert.True(defaults.GetProperty("notifyFollows").GetBoolean());
        Assert.Equal(HttpStatusCode.BadRequest, (await _api.SendAsync(HttpMethod.Put, "/api/v1/users/me/settings", a.AccessToken, new { notifyLikes = true, notifyComments = true, notifyFollows = true, extra = 1 })).StatusCode);

        var saved = await JsonAsync(await _api.SendAsync(HttpMethod.Put, "/api/v1/users/me/settings", a.AccessToken, new { notifyLikes = true, notifyComments = true, notifyFollows = false }));
        Assert.False(saved.GetProperty("notifyFollows").GetBoolean());

        await _api.SendAsync(HttpMethod.Put, $"/api/v1/users/{a.User.Id}/follow", b);
        var off = await JsonAsync(await _api.SendAsync(HttpMethod.Get, "/api/v1/notifications", a.AccessToken));
        Assert.Empty(off.GetProperty("items").EnumerateArray());

        // Turned back on: new follows notify again.
        await _api.SendAsync(HttpMethod.Put, "/api/v1/users/me/settings", a.AccessToken, new { notifyLikes = true, notifyComments = true, notifyFollows = true });
        var c = await _api.RegisterAsync();
        await _api.SendAsync(HttpMethod.Put, $"/api/v1/users/{a.User.Id}/follow", c);
        var on = await JsonAsync(await _api.SendAsync(HttpMethod.Get, "/api/v1/notifications", a.AccessToken));
        Assert.Single(on.GetProperty("items").EnumerateArray());
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private string LatestCodeFor(string email)
    {
        var file = Directory.GetFiles(_factory.Outbox, "*.txt")
            .Select(path => (Path: path, Text: File.ReadAllText(path)))
            .Where(item => item.Text.Contains($"To: {email}", StringComparison.Ordinal))
            .OrderByDescending(item => File.GetLastWriteTimeUtc(item.Path))
            .First();
        return System.Text.RegularExpressions.Regex.Match(file.Text, @"\b\d{6}\b").Value;
    }

    private async Task<string> AnyCategorySlugAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.Categories.OrderBy(category => category.Slug).Select(category => category.Slug).FirstAsync();
    }

    private string AvatarFile(string url) =>
        Path.Combine(_factory.PhotoRoot, "avatars", url[(url.IndexOf("?v=", StringComparison.Ordinal) + 3)..] + ".jpg");

    private static byte[] Jpeg(int width, int height)
    {
        using var bitmap = new SkiaSharp.SKBitmap(width, height);
        bitmap.Erase(SkiaSharp.SKColors.SeaGreen);
        using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Jpeg, 90);
        return data.ToArray();
    }

    private async Task<HttpResponseMessage> UploadAvatarAsync(string user, byte[] bytes)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        form.Add(file, "file", "avatar.jpg");
        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/users/me/avatar") { Content = form };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", user);
        return await _factory.CreateClient().SendAsync(request);
    }

    private async Task<Guid> InsertPartnerRestaurantAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var restaurant = new Insaaf.Domain.Entities.Restaurant
        {
            Id = Guid.NewGuid(),
            Name = $"phase11-{Guid.NewGuid():N}",
            Description = "وصف",
            City = "Amman",
            Latitude = 31.95,
            Longitude = 35.91,
            PriceLevel = Insaaf.Domain.Enums.PriceLevel.Moderate,
            IsActive = true,
            IsPartner = true,
            CreatedAt = DateTime.UtcNow,
        };
        context.Restaurants.Add(restaurant);
        await context.SaveChangesAsync();
        return restaurant.Id;
    }

    private async Task<Guid> UploadAsync(string user, string token)
    {
        using var bitmap = new SkiaSharp.SKBitmap(64, 48);
        bitmap.Erase(SkiaSharp.SKColors.Teal);
        using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Jpeg, 90);
        using var form = new MultipartFormDataContent
        {
            { new StringContent(token), "token" },
            { new StringContent(Guid.NewGuid().ToString()), "clientUploadId" },
        };
        var file = new ByteArrayContent(data.ToArray());
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        form.Add(file, "file", "photo.jpg");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/verification/photos") { Content = form };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", user);
        var response = await _factory.CreateClient().SendAsync(request);
        return (await JsonAsync(response)).GetProperty("id").GetGuid();
    }

    private async Task SetRestaurantActiveAsync(Guid restaurantId, bool isActive)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var restaurant = await context.Restaurants.SingleAsync(item => item.Id == restaurantId);
        restaurant.IsActive = isActive;
        await context.SaveChangesAsync();
    }

    internal static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        var raw = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {raw}");
        return JsonDocument.Parse(raw).RootElement.GetProperty("data").Clone();
    }
}
