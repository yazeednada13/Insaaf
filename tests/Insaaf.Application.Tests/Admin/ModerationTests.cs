using Insaaf.Application.Admin;
using Insaaf.Application.Admin.Dtos;
using Insaaf.Application.Auth.Abstractions;
using Insaaf.Application.Restaurants;
using Insaaf.Application.Reviews;
using Insaaf.Application.Reviews.Dtos;
using Insaaf.Application.Social;
using Insaaf.Application.Social.Dtos;
using Insaaf.Application.Tests.Social;
using Insaaf.Domain.Constants;
using Insaaf.Domain.Entities;
using Insaaf.Domain.Enums;
using Insaaf.Infrastructure.Auth;
using Insaaf.Infrastructure.Identity;
using Insaaf.Infrastructure.Persistence;
using Insaaf.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Insaaf.Application.Tests.Admin;

/// <summary>Phase 9, tasks 9.2b–9.5: hiding reviews, deleting comments, suspending accounts, admin lists.</summary>
public sealed class ModerationTests
{
    private static readonly DateTime Now = new(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _databaseName = $"moderation-{Guid.NewGuid():N}";
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly RecordingAuditLog _audit = new();
    private readonly Guid _adminId = Guid.NewGuid();
    private readonly Guid _authorId = Guid.NewGuid();
    private readonly Guid _readerId = Guid.NewGuid();
    private readonly Guid _restaurantId = Guid.NewGuid();
    private readonly Guid _hiddenReviewId = Guid.NewGuid();
    private readonly Guid _otherReviewId = Guid.NewGuid();
    private readonly Guid _visitTokenId = Guid.NewGuid();
    private readonly Guid _categoryId = Guid.NewGuid();

    public ModerationTests()
    {
        _clock.Setup(clock => clock.UtcNow).Returns(Now);
        var adminRoleId = Guid.NewGuid();
        using var context = CreateContext();
        context.Users.AddRange(
            new ApplicationUser { Id = _adminId, UserName = "admin", NormalizedUserName = "ADMIN", Email = "admin@example.com", NormalizedEmail = "ADMIN@EXAMPLE.COM" },
            new ApplicationUser { Id = _authorId, UserName = "author", NormalizedUserName = "AUTHOR", FullName = "كاتب", Email = "author@example.com", NormalizedEmail = "AUTHOR@EXAMPLE.COM" },
            new ApplicationUser { Id = _readerId, UserName = "reader", NormalizedUserName = "READER", FullName = "قارئ", Email = "reader@example.com", NormalizedEmail = "READER@EXAMPLE.COM" });
        context.Roles.Add(new IdentityRole<Guid> { Id = adminRoleId, Name = Roles.Admin, NormalizedName = "ADMIN" });
        context.UserRoles.Add(new IdentityUserRole<Guid> { UserId = _adminId, RoleId = adminRoleId });
        context.Categories.Add(new Category { Id = _categoryId, Name = "برجر", Slug = "burger" });
        context.Restaurants.Add(new Restaurant
        {
            Id = _restaurantId,
            Name = "مطعم",
            Description = "وصف",
            City = "Amman",
            Latitude = 31.95,
            Longitude = 35.91,
            PriceLevel = PriceLevel.Moderate,
            IsActive = true,
            CreatedAt = Now.AddDays(-30),
        });
        context.RestaurantCategories.Add(new RestaurantCategory { RestaurantId = _restaurantId, CategoryId = _categoryId });
        context.Reviews.AddRange(
            CreateReview(_hiddenReviewId, rating: 1, daysAgo: 1, _visitTokenId),
            CreateReview(_otherReviewId, rating: 5, daysAgo: 2, Guid.NewGuid()));
        context.UserFollows.Add(new UserFollow { FollowerId = _readerId, FolloweeId = _authorId, CreatedAt = Now.AddDays(-5) });
        context.SaveChanges();
    }

    // ── 9.2b Hide and unhide ──────────────────────────────────────────────────

    [Fact]
    public async Task Hide_RemovesTheReviewFromRatingsAndEveryPublicRead()
    {
        await using var context = CreateContext();
        await CreateCommentService(context).CreateAsync(_hiddenReviewId, _readerId, new CreateReviewCommentRequest("تعليق"));
        await CreateLikeService(context).LikeAsync(_hiddenReviewId, _readerId);
        Assert.Equal(3m, (await RestaurantDetail(context)).AverageRating);

        var hidden = await CreateModeration(context).SetVisibilityAsync(
            _adminId, _hiddenReviewId, new SetReviewVisibilityRequest(true, "spam"));

        Assert.True(hidden.IsHidden);
        Assert.Equal("spam", hidden.HiddenReason);

        var detail = await RestaurantDetail(context);
        Assert.Equal(5m, detail.AverageRating);
        Assert.Equal(1, detail.ReviewCount);

        var reviews = CreateReviewService(context);
        var list = await reviews.ListForRestaurantAsync(_restaurantId, new RestaurantReviewsQuery(), _readerId);
        Assert.DoesNotContain(list.Items, item => item.Id == _hiddenReviewId);
        var missing = await Assert.ThrowsAsync<SocialException>(() => reviews.GetByIdAsync(_hiddenReviewId, _readerId));
        Assert.Equal("not_found", missing.Code);

        var feed = await new FeedService(new ReviewRepository(context)).GetAsync(_readerId, new FeedQuery());
        Assert.DoesNotContain(feed.Items, item => item.Id == _hiddenReviewId);

        // Notifications about a review that isn't public are hidden too (Phase 8A rule).
        var notifications = await new NotificationService(
                new NotificationRepository(context, NullLogger<NotificationRepository>.Instance), _clock.Object)
            .ListAsync(_authorId, new NotificationsQuery());
        Assert.Empty(notifications.Items);
    }

    [Fact]
    public async Task HiddenReview_CantBeLikedUnlikedCommentedOrHaveItsCommentsListed()
    {
        await using var context = CreateContext();
        await CreateModeration(context).SetVisibilityAsync(_adminId, _hiddenReviewId, new SetReviewVisibilityRequest(true, null));
        var likes = CreateLikeService(context);
        var comments = CreateCommentService(context);

        var errors = new[]
        {
            await Assert.ThrowsAsync<SocialException>(() => likes.LikeAsync(_hiddenReviewId, _readerId)),
            await Assert.ThrowsAsync<SocialException>(() => likes.UnlikeAsync(_hiddenReviewId, _readerId)),
            await Assert.ThrowsAsync<SocialException>(() =>
                comments.CreateAsync(_hiddenReviewId, _readerId, new CreateReviewCommentRequest("تعليق"))),
            await Assert.ThrowsAsync<SocialException>(() =>
                comments.ListAsync(_hiddenReviewId, _readerId, new ReviewCommentsQuery())),
        };

        Assert.All(errors, error => Assert.Equal("not_found", error.Code));
        Assert.Empty(await context.ReviewLikes.ToListAsync());
        Assert.Empty(await context.ReviewComments.ToListAsync());
    }

    [Fact]
    public async Task Hide_KeepsTheVisitTokenRedeemed()
    {
        await using var context = CreateContext();

        await CreateModeration(context).SetVisibilityAsync(_adminId, _hiddenReviewId, new SetReviewVisibilityRequest(true, null));

        var review = await context.Reviews.SingleAsync(item => item.Id == _hiddenReviewId);
        Assert.Equal(_visitTokenId, review.VisitTokenId);
        Assert.True(review.IsVerified);
        Assert.Equal(_adminId, review.HiddenByUserId);
    }

    [Fact]
    public async Task Unhide_RestoresVisibilityRatingsAndInteraction()
    {
        await using var context = CreateContext();
        var moderation = CreateModeration(context);
        await moderation.SetVisibilityAsync(_adminId, _hiddenReviewId, new SetReviewVisibilityRequest(true, "spam"));

        var shown = await moderation.SetVisibilityAsync(_adminId, _hiddenReviewId, new SetReviewVisibilityRequest(false, null));

        Assert.False(shown.IsHidden);
        Assert.Null(shown.HiddenReason);
        Assert.Equal(3m, (await RestaurantDetail(context)).AverageRating);
        Assert.Equal(_hiddenReviewId, (await CreateReviewService(context).GetByIdAsync(_hiddenReviewId, _readerId)).Id);
        Assert.True((await CreateLikeService(context).LikeAsync(_hiddenReviewId, _readerId)).IsLikedByMe);
        await CreateCommentService(context).CreateAsync(_hiddenReviewId, _readerId, new CreateReviewCommentRequest("رجع"));
        Assert.Single(await context.ReviewComments.ToListAsync());
        Assert.Null((await context.Reviews.SingleAsync(item => item.Id == _hiddenReviewId)).HiddenByUserId);
    }

    [Fact]
    public async Task Hide_IsIdempotent_AndAuditsWithoutTheReason()
    {
        await using var context = CreateContext();
        var moderation = CreateModeration(context);

        await moderation.SetVisibilityAsync(_adminId, _hiddenReviewId, new SetReviewVisibilityRequest(true, "سبب سري"));
        await moderation.SetVisibilityAsync(_adminId, _hiddenReviewId, new SetReviewVisibilityRequest(true, "سبب آخر"));

        Assert.Equal(
            [
                new AdminAuditEntry("review.hide", _adminId, "review", _hiddenReviewId, Now, AdminAuditResult.Succeeded),
                new AdminAuditEntry("review.hide", _adminId, "review", _hiddenReviewId, Now, AdminAuditResult.NoChange),
            ],
            _audit.Entries);
        Assert.Equal("سبب سري", (await context.Reviews.SingleAsync(item => item.Id == _hiddenReviewId)).HiddenReason);
    }

    [Fact]
    public async Task Visibility_WithoutHiddenOrForAMissingReview_IsRefused()
    {
        await using var context = CreateContext();
        var moderation = CreateModeration(context);

        var missingField = await Assert.ThrowsAsync<AdminException>(() =>
            moderation.SetVisibilityAsync(_adminId, _hiddenReviewId, new SetReviewVisibilityRequest(null, null)));
        var missingReview = await Assert.ThrowsAsync<AdminException>(() =>
            moderation.SetVisibilityAsync(_adminId, Guid.NewGuid(), new SetReviewVisibilityRequest(true, null)));
        var longReason = await Assert.ThrowsAsync<AdminException>(() =>
            moderation.SetVisibilityAsync(_adminId, _hiddenReviewId, new SetReviewVisibilityRequest(true, new string('x', 501))));

        Assert.Equal("validation_error", missingField.Code);
        Assert.Equal("not_found", missingReview.Code);
        Assert.Equal("validation_error", longReason.Code);
    }

    // ── 9.3 Comments ──────────────────────────────────────────────────────────

    [Fact]
    public async Task AdminDeletesAnyComment_AndItsNotification()
    {
        await using var context = CreateContext();
        var comment = await CreateCommentService(context).CreateAsync(_otherReviewId, _readerId, new CreateReviewCommentRequest("سيء"));
        Assert.Single(await context.Notifications.ToListAsync());

        var deleted = await CreateModeration(context).DeleteCommentAsync(_adminId, comment.Id);

        Assert.True(deleted.Deleted);
        Assert.Empty(await context.ReviewComments.ToListAsync());
        Assert.Empty(await context.Notifications.ToListAsync());
        Assert.Equal(new AdminAuditEntry("comment.delete", _adminId, "comment", comment.Id, Now, AdminAuditResult.Succeeded), _audit.Entries.Single());
    }

    [Fact]
    public async Task AdminDeletesACommentOnAHiddenReview()
    {
        await using var context = CreateContext();
        var comment = await CreateCommentService(context).CreateAsync(_hiddenReviewId, _readerId, new CreateReviewCommentRequest("سيء"));
        var moderation = CreateModeration(context);
        await moderation.SetVisibilityAsync(_adminId, _hiddenReviewId, new SetReviewVisibilityRequest(true, null));

        await moderation.DeleteCommentAsync(_adminId, comment.Id);

        Assert.Empty(await context.ReviewComments.ToListAsync());
        var missing = await Assert.ThrowsAsync<AdminException>(() => moderation.DeleteCommentAsync(_adminId, comment.Id));
        Assert.Equal("not_found", missing.Code);
    }

    // ── 9.4 Suspension ────────────────────────────────────────────────────────

    [Fact]
    public async Task Suspend_RevokesRefreshTokens_AndRefreshIsRefused()
    {
        await using var context = CreateContext();
        var tokens = CreateRefreshTokenService(context);
        var issued = await tokens.IssueAsync(_authorId);

        var user = await CreateUserModeration(context, tokens).SetSuspensionAsync(_adminId, _authorId, new SetSuspensionRequest(true));

        Assert.True(user.IsSuspended);
        Assert.All(await context.RefreshTokens.ToListAsync(), token => Assert.NotNull(token.RevokedAt));
        var rotation = await tokens.RotateAsync(issued.Token);
        Assert.False(rotation.Succeeded);
        Assert.Equal(RefreshTokenFailureReason.Invalid, rotation.FailureReason);
        Assert.Equal(new AdminAuditEntry("user.suspend", _adminId, "user", _authorId, Now, AdminAuditResult.Succeeded), _audit.Entries.Single());
    }

    [Fact]
    public async Task Refresh_OfASuspendedAccountWithALiveToken_IsRefusedAsSuspended()
    {
        await using var context = CreateContext();
        var tokens = CreateRefreshTokenService(context);
        var issued = await tokens.IssueAsync(_authorId);
        var author = await context.Users.SingleAsync(item => item.Id == _authorId);
        author.SuspendedAt = Now; // e.g. a token issued between suspension and revocation
        await context.SaveChangesAsync();

        var rotation = await tokens.RotateAsync(issued.Token);

        Assert.False(rotation.Succeeded);
        Assert.Equal(RefreshTokenFailureReason.Suspended, rotation.FailureReason);
    }

    [Fact]
    public async Task Unsuspend_KeepsAnExistingFailedLoginLockout()
    {
        await using var context = CreateContext();
        var lockoutEnd = new DateTimeOffset(Now.AddMinutes(5));
        var author = await context.Users.SingleAsync(item => item.Id == _authorId);
        author.LockoutEnabled = true;
        author.LockoutEnd = lockoutEnd;
        author.AccessFailedCount = 4;
        await context.SaveChangesAsync();
        var moderation = CreateUserModeration(context, CreateRefreshTokenService(context));

        await moderation.SetSuspensionAsync(_adminId, _authorId, new SetSuspensionRequest(true));
        var restored = await moderation.SetSuspensionAsync(_adminId, _authorId, new SetSuspensionRequest(false));

        Assert.False(restored.IsSuspended);
        await using var check = CreateContext();
        var stored = await check.Users.SingleAsync(item => item.Id == _authorId);
        Assert.Equal(lockoutEnd, stored.LockoutEnd);
        Assert.Equal(4, stored.AccessFailedCount);
        Assert.Null(stored.SuspendedAt);
        Assert.Null(stored.SuspendedByUserId);
    }

    [Fact]
    public async Task Suspend_OfAnAdminOrAMissingUser_IsRefused()
    {
        await using var context = CreateContext();
        var moderation = CreateUserModeration(context, CreateRefreshTokenService(context));

        var admin = await Assert.ThrowsAsync<AdminException>(() =>
            moderation.SetSuspensionAsync(_adminId, _adminId, new SetSuspensionRequest(true)));
        var missing = await Assert.ThrowsAsync<AdminException>(() =>
            moderation.SetSuspensionAsync(_adminId, Guid.NewGuid(), new SetSuspensionRequest(true)));
        var noField = await Assert.ThrowsAsync<AdminException>(() =>
            moderation.SetSuspensionAsync(_adminId, _authorId, new SetSuspensionRequest(null)));

        Assert.Equal(("admin_target", 403), (admin.Code, admin.StatusCode));
        Assert.Equal("not_found", missing.Code);
        Assert.Equal("validation_error", noField.Code);
        Assert.Equal(AdminAuditResult.Refused, _audit.Entries[0].Result);
    }

    // ── 9.5 Admin lists ───────────────────────────────────────────────────────

    [Fact]
    public async Task AdminReviewList_IncludesHiddenReviews_WithFiltersAndPaging()
    {
        await using var context = CreateContext();
        var moderation = CreateModeration(context);
        await moderation.SetVisibilityAsync(_adminId, _hiddenReviewId, new SetReviewVisibilityRequest(true, null));

        var first = await moderation.ListAsync(new AdminReviewListQuery(RestaurantId: _restaurantId, Limit: 1));
        var second = await moderation.ListAsync(new AdminReviewListQuery(RestaurantId: _restaurantId, Cursor: first.NextCursor, Limit: 1));
        var byOtherUser = await moderation.ListAsync(new AdminReviewListQuery(UserId: _readerId));

        Assert.Equal(_hiddenReviewId, first.Items.Single().Id);
        Assert.True(first.Items.Single().IsHidden);
        Assert.Equal("author", first.Items.Single().AuthorUserName);
        Assert.Equal(_otherReviewId, second.Items.Single().Id);
        Assert.Null(second.NextCursor);
        Assert.Empty(byOtherUser.Items);
        await Assert.ThrowsAsync<AdminException>(() => moderation.ListAsync(new AdminReviewListQuery(Cursor: "bad")));
    }

    [Fact]
    public async Task AdminUserSearch_FindsByEmailOrUsername_AndMarksAdmins()
    {
        await using var context = CreateContext();
        var moderation = CreateUserModeration(context, CreateRefreshTokenService(context));

        var byEmail = await moderation.SearchAsync("reader@");
        var byName = await moderation.SearchAsync("ADM");

        Assert.Equal(_readerId, byEmail.Items.Single().Id);
        Assert.True(byName.Items.Single().IsAdmin);
        await Assert.ThrowsAsync<AdminException>(() => moderation.SearchAsync("a"));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Task<Insaaf.Application.Restaurants.Dtos.RestaurantDetailDto> RestaurantDetail(AppDbContext context) =>
        new RestaurantService(new RestaurantRepository(context), _clock.Object).GetByIdAsync(_restaurantId);

    private ReviewModerationService CreateModeration(AppDbContext context) =>
        new(new ReviewRepository(context), new ReviewCommentRepository(context), TestNotifications.Publisher(context, _clock.Object), _clock.Object, _audit);

    private UserModerationService CreateUserModeration(AppDbContext context, IRefreshTokenService tokens) =>
        new(new UserModerationRepository(context), tokens, _clock.Object, _audit);

    private RefreshTokenService CreateRefreshTokenService(AppDbContext context) =>
        new(context, Options.Create(new JwtOptions { RefreshTokenDays = 7 }), _clock.Object);

    private ReviewService CreateReviewService(AppDbContext context) =>
        new(new ReviewRepository(context), new RestaurantRepository(context));

    private ReviewLikeService CreateLikeService(AppDbContext context) =>
        new(new ReviewRepository(context), new ReviewLikeRepository(context), _clock.Object, TestNotifications.Publisher(context, _clock.Object));

    private ReviewCommentService CreateCommentService(AppDbContext context) =>
        new(new ReviewRepository(context), new ReviewCommentRepository(context), _clock.Object, TestNotifications.Publisher(context, _clock.Object));

    private Review CreateReview(Guid id, int rating, int daysAgo, Guid visitTokenId) =>
        new()
        {
            Id = id,
            RestaurantId = _restaurantId,
            UserId = _authorId,
            Rating = rating,
            FoodRating = rating,
            ServiceRating = rating,
            ValueRating = rating,
            AtmosphereRating = rating,
            Text = "تقييم",
            IsVerified = true,
            VisitTokenId = visitTokenId,
            CreatedAt = Now.AddDays(-daysAgo),
        };

    private AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_databaseName).Options);
}
