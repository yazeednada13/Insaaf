using Insaaf.Application.Auth.Abstractions;
using Insaaf.Application.Reviews;
using Insaaf.Application.Social;
using Insaaf.Application.Social.Dtos;
using Insaaf.Domain.Constants;
using Insaaf.Domain.Entities;
using Insaaf.Domain.Enums;
using Insaaf.Infrastructure.Identity;
using Insaaf.Infrastructure.Persistence;
using Insaaf.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Insaaf.Application.Tests.Social;

/// <summary>Profiles, lists and search on the real repositories (InMemory).</summary>
public sealed class UserProfileTests
{
    private static readonly DateTime Now = new(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _databaseName = $"profiles-{Guid.NewGuid():N}";
    private readonly Mock<IUserAccountService> _accounts = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _otherId = Guid.NewGuid();
    private readonly Guid _thirdId = Guid.NewGuid();
    private readonly Guid _adminId = Guid.NewGuid();
    private readonly Guid _restaurantId = Guid.NewGuid();
    private readonly Guid _inactiveRestaurantId = Guid.NewGuid();

    public UserProfileTests()
    {
        _clock.Setup(clock => clock.UtcNow).Returns(Now);
        var adminRoleId = Guid.NewGuid();
        using var context = CreateContext();
        context.Users.AddRange(
            new ApplicationUser { Id = _userId, UserName = "sara.eats", FullName = "سارة منصور", Email = "s@example.com", Bio = "نبذة" },
            new ApplicationUser { Id = _otherId, UserName = "omar", FullName = "عمر", Email = "o@example.com" },
            new ApplicationUser { Id = _thirdId, UserName = "layla", FullName = "ليلى", Email = "l@example.com" },
            new ApplicationUser { Id = _adminId, UserName = "ops.admin", FullName = "سارة الإدارة", Email = "a@example.com" });
        context.Roles.Add(new IdentityRole<Guid> { Id = adminRoleId, Name = Roles.Admin, NormalizedName = "ADMIN" });
        context.UserRoles.Add(new IdentityUserRole<Guid> { UserId = _adminId, RoleId = adminRoleId });
        context.Restaurants.AddRange(
            CreateRestaurant(_restaurantId, isActive: true),
            CreateRestaurant(_inactiveRestaurantId, isActive: false));
        context.Reviews.AddRange(
            CreateReview(_userId, _restaurantId, isVerified: true, 0),
            CreateReview(_userId, _restaurantId, isVerified: true, 1),
            CreateReview(_userId, _restaurantId, isVerified: false, 2),
            CreateReview(_userId, _inactiveRestaurantId, isVerified: true, 3));
        context.SaveChanges();
    }

    // ── Profile ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Profile_CountsOnlyPublicReviewsOfActiveRestaurants()
    {
        await using var context = CreateContext();

        var profile = await CreateService(context).GetAsync(_userId, _otherId);

        Assert.Equal(2, profile.ReviewCount);
        Assert.Equal("نبذة", profile.Bio);
        Assert.False(profile.IsMe);
    }

    [Fact]
    public async Task Profile_ShowsFollowCountsAndStatus_LeavingAdminsOut()
    {
        await SeedFollowsAsync((_otherId, _userId), (_thirdId, _userId), (_adminId, _userId), (_userId, _thirdId));
        await using var context = CreateContext();

        var profile = await CreateService(context).GetAsync(_userId, _otherId);

        Assert.Equal(2, profile.FollowerCount);
        Assert.Equal(1, profile.FollowingCount);
        Assert.True(profile.IsFollowedByMe);
    }

    [Fact]
    public async Task Admin_HasNoPublicProfile_ButSeesTheirOwn()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        var error = await Assert.ThrowsAsync<SocialException>(() => service.GetAsync(_adminId, _userId));
        var own = await service.GetMineAsync(_adminId);

        Assert.Equal("not_found", error.Code);
        Assert.Equal(_adminId, own.Id);
        Assert.True(own.IsMe);
    }

    [Fact]
    public async Task AdminLists_AndReviews_AreNotFoundForOthers()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        await Assert.ThrowsAsync<SocialException>(() => service.ListFollowersAsync(_adminId, _userId, new UserListQuery()));
        await Assert.ThrowsAsync<SocialException>(() => service.ListFollowingAsync(_adminId, _userId, new UserListQuery()));
        await Assert.ThrowsAsync<SocialException>(() => service.ListReviewsAsync(_adminId, _userId, new FeedQuery()));
    }

    [Fact]
    public async Task Follow_TreatsAdminAsUnknown()
    {
        await using var context = CreateContext();
        var follows = new UserFollowService(new UserFollowRepository(context), new UserProfileRepository(context), _clock.Object);

        var error = await Assert.ThrowsAsync<SocialException>(() => follows.FollowAsync(_userId, _adminId));

        Assert.Equal("not_found", error.Code);
    }

    // ── A user's reviews ──────────────────────────────────────────────────────

    [Fact]
    public async Task Reviews_ExcludeHiddenAndInactive_NewestFirst_WithCursor()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        var first = await service.ListReviewsAsync(_userId, _otherId, new FeedQuery(Limit: 1));
        var second = await service.ListReviewsAsync(_userId, _otherId, new FeedQuery(first.NextCursor, Limit: 1));

        Assert.NotNull(first.NextCursor);
        Assert.Null(second.NextCursor);
        Assert.True(first.Items[0].CreatedAt > second.Items[0].CreatedAt);
        Assert.All(first.Items.Concat(second.Items), item => Assert.Equal(_restaurantId, item.Restaurant.Id));
    }

    // ── Followers and following ───────────────────────────────────────────────

    [Fact]
    public async Task Lists_PageDeterministically_WhenCreatedAtTies()
    {
        await SeedFollowsAsync((_otherId, _userId), (_thirdId, _userId));
        await using var context = CreateContext();
        var service = CreateService(context);

        var first = await service.ListFollowersAsync(_userId, _userId, new UserListQuery(1, 1));
        var second = await service.ListFollowersAsync(_userId, _userId, new UserListQuery(2, 1));
        var again = await service.ListFollowersAsync(_userId, _userId, new UserListQuery(1, 1));

        Assert.Equal(2, first.TotalCount);
        Assert.Equal(first.Items[0].Id, again.Items[0].Id);
        Assert.NotEqual(first.Items[0].Id, second.Items[0].Id);
    }

    [Fact]
    public async Task Lists_LeaveAdminsOut_AndShowTheViewersStatus()
    {
        await SeedFollowsAsync((_otherId, _userId), (_adminId, _userId), (_thirdId, _otherId));
        await using var context = CreateContext();
        var service = CreateService(context);

        var followers = await service.ListFollowersAsync(_userId, _thirdId, new UserListQuery());

        var row = Assert.Single(followers.Items);
        Assert.Equal(_otherId, row.Id);
        Assert.True(row.IsFollowedByMe);
        Assert.Equal(1, followers.TotalCount);
    }

    [Fact]
    public async Task Following_ListsWhoTheUserFollows()
    {
        await SeedFollowsAsync((_userId, _otherId));
        await using var context = CreateContext();

        var following = await CreateService(context).ListFollowingAsync(_userId, _userId, new UserListQuery());

        Assert.Equal(_otherId, Assert.Single(following.Items).Id);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 51)]
    public async Task Lists_RejectBadPaging(int page, int pageSize)
    {
        await using var context = CreateContext();

        var error = await Assert.ThrowsAsync<SocialException>(() =>
            CreateService(context).ListFollowersAsync(_userId, _userId, new UserListQuery(page, pageSize)));

        Assert.Equal("validation_error", error.Code);
    }

    // ── Search ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("سارة")]
    [InlineData("منصور")]
    [InlineData("sara")]
    public async Task Search_MatchesNameOrUsername_AndHidesAdmins(string term)
    {
        await using var context = CreateContext();

        var result = await CreateService(context).SearchAsync(_otherId, new UserSearchQuery(term));

        var item = Assert.Single(result.Items);
        Assert.Equal(_userId, item.Id);
        Assert.Equal(2, item.ReviewCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" س ")]
    public async Task Search_RejectsShortTerms(string? term)
    {
        await using var context = CreateContext();

        var error = await Assert.ThrowsAsync<SocialException>(() =>
            CreateService(context).SearchAsync(_userId, new UserSearchQuery(term)));

        Assert.Equal("validation_error", error.Code);
    }

    // ── Editing ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null, "name", null)]
    [InlineData("   ", "name", null)]
    [InlineData("Name", "  ", null)]
    public async Task Update_RejectsMissingFields_WithoutCallingIdentity(string? fullName, string? userName, string? bio)
    {
        await using var context = CreateContext();

        var error = await Assert.ThrowsAsync<SocialException>(() =>
            CreateService(context).UpdateMineAsync(_userId, new UpdateProfileRequest(fullName, userName, bio)));

        Assert.Equal("validation_error", error.Code);
        _accounts.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(150, true)]
    [InlineData(151, false)]
    public async Task Update_EnforcesBioLength(int length, bool valid)
    {
        SetupUpdate(ProfileUpdateResult.Updated);
        await using var context = CreateContext();
        var service = CreateService(context);
        var request = new UpdateProfileRequest("Name", "name", new string('ن', length));

        if (valid)
        {
            await service.UpdateMineAsync(_userId, request);
        }
        else
        {
            var error = await Assert.ThrowsAsync<SocialException>(() => service.UpdateMineAsync(_userId, request));
            Assert.Equal("validation_error", error.Code);
        }
    }

    [Fact]
    public async Task Update_TrimsFields_AndClearsAnEmptyBio()
    {
        SetupUpdate(ProfileUpdateResult.Updated);
        await using var context = CreateContext();

        await CreateService(context).UpdateMineAsync(_userId, new UpdateProfileRequest("  سارة  ", " sara ", "   "));

        _accounts.Verify(service => service.UpdateProfileAsync(_userId, "سارة", "sara", null, It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Update_MapsUsernameTaken_ToConflict()
    {
        SetupUpdate(ProfileUpdateResult.UsernameTaken);
        await using var context = CreateContext();

        var error = await Assert.ThrowsAsync<SocialException>(() =>
            CreateService(context).UpdateMineAsync(_userId, new UpdateProfileRequest("Name", "omar", null)));

        Assert.Equal("username_taken", error.Code);
        Assert.Equal(SocialStatusCodes.Conflict, error.StatusCode);
    }

    // ── Admin names in content ────────────────────────────────────────────────

    [Fact]
    public async Task AdminComment_ShowsTheName_WithNoPublicProfile()
    {
        await using var context = CreateContext();
        var reviewId = (await context.Reviews.FirstAsync(review => review.IsVerified && review.RestaurantId == _restaurantId)).Id;
        var comments = new ReviewCommentService(new ReviewRepository(context), new ReviewCommentRepository(context), _clock.Object);

        var admin = await comments.CreateAsync(reviewId, _adminId, new CreateReviewCommentRequest("تعليق"));
        var user = await comments.CreateAsync(reviewId, _otherId, new CreateReviewCommentRequest("تعليق"));

        Assert.Equal("سارة الإدارة", admin.Author.FullName);
        Assert.False(admin.Author.HasPublicProfile);
        Assert.True(user.Author.HasPublicProfile);
    }

    [Fact]
    public async Task AdminReview_HasNoPublicProfile_OnListAndDetail()
    {
        await using var seed = CreateContext();
        var review = CreateReview(_adminId, _restaurantId, isVerified: true, 9);
        seed.Reviews.Add(review);
        await seed.SaveChangesAsync();

        await using var context = CreateContext();
        var reviews = new ReviewService(new ReviewRepository(context), new RestaurantRepository(context));

        var list = await reviews.ListForRestaurantAsync(_restaurantId, new(), _userId);
        var detail = await reviews.GetByIdAsync(review.Id, _userId);

        Assert.False(list.Items.Single(item => item.Id == review.Id).Reviewer.HasPublicProfile);
        Assert.False(detail.Reviewer.HasPublicProfile);
        Assert.True(list.Items.First(item => item.Id != review.Id).Reviewer.HasPublicProfile);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SetupUpdate(ProfileUpdateResult result) =>
        _accounts
            .Setup(service => service.UpdateProfileAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

    private async Task SeedFollowsAsync(params (Guid Follower, Guid Followee)[] follows)
    {
        await using var context = CreateContext();
        context.UserFollows.AddRange(follows.Select(follow => new UserFollow
        {
            FollowerId = follow.Follower,
            FolloweeId = follow.Followee,
            CreatedAt = Now,
        }));
        await context.SaveChangesAsync();
    }

    private UserProfileService CreateService(AppDbContext context) =>
        new(new UserProfileRepository(context), new ReviewRepository(context), _accounts.Object);

    private AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_databaseName).Options);

    private static Restaurant CreateRestaurant(Guid id, bool isActive) =>
        new()
        {
            Id = id,
            Name = $"restaurant-{id:N}",
            Description = "وصف",
            City = "عمّان",
            Latitude = 31.95,
            Longitude = 35.92,
            PriceLevel = PriceLevel.Moderate,
            IsActive = isActive,
            CreatedAt = Now,
        };

    private static Review CreateReview(Guid userId, Guid restaurantId, bool isVerified, int minutes) =>
        new()
        {
            Id = Guid.NewGuid(),
            RestaurantId = restaurantId,
            UserId = userId,
            Rating = 5,
            FoodRating = 4,
            ServiceRating = 4,
            ValueRating = 4,
            AtmosphereRating = 4,
            Text = "تقييم",
            IsVerified = isVerified,
            VisitTokenId = isVerified ? Guid.NewGuid() : null,
            CreatedAt = Now.AddMinutes(minutes),
        };
}
