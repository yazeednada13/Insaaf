using Insaaf.Application.Auth.Abstractions;
using Insaaf.Application.Reviews;
using Insaaf.Application.Social;
using Insaaf.Application.Social.Dtos;
using Insaaf.Domain.Entities;
using Insaaf.Domain.Enums;
using Insaaf.Infrastructure.Identity;
using Insaaf.Infrastructure.Persistence;
using Insaaf.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Insaaf.Application.Tests.Social;

/// <summary>Following and the cursor feed on the real repositories (InMemory).</summary>
public sealed class FollowAndFeedTests
{
    private static readonly DateTime Now = new(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _databaseName = $"feed-{Guid.NewGuid():N}";
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Guid _viewerId = Guid.NewGuid();
    private readonly Guid _reviewerId = Guid.NewGuid();
    private readonly Guid _strangerId = Guid.NewGuid();
    private readonly Guid _restaurantId = Guid.NewGuid();
    private readonly Guid _inactiveRestaurantId = Guid.NewGuid();

    public FollowAndFeedTests()
    {
        _clock.Setup(clock => clock.UtcNow).Returns(Now);
        using var context = CreateContext();
        context.Users.AddRange(
            new ApplicationUser { Id = _viewerId, UserName = "viewer", FullName = "Viewer", Email = "v@example.com" },
            new ApplicationUser { Id = _reviewerId, UserName = "reviewer", FullName = "Reviewer", Email = "r@example.com" },
            new ApplicationUser { Id = _strangerId, UserName = "stranger", FullName = "Stranger", Email = "s@example.com" });
        context.Restaurants.AddRange(
            CreateRestaurant(_restaurantId, isActive: true),
            CreateRestaurant(_inactiveRestaurantId, isActive: false));
        context.SaveChanges();
    }

    // ── Following ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Follow_Twice_KeepsOneRow()
    {
        await using var context = CreateContext();
        var service = CreateFollowService(context);

        await service.FollowAsync(_viewerId, _reviewerId);
        var state = await service.FollowAsync(_viewerId, _reviewerId);

        Assert.True(state.IsFollowedByMe);
        Assert.Equal(1, await context.UserFollows.CountAsync());
    }

    [Fact]
    public async Task Unfollow_IsRepeatSafe()
    {
        await using var context = CreateContext();
        var service = CreateFollowService(context);
        await service.FollowAsync(_viewerId, _reviewerId);

        await service.UnfollowAsync(_viewerId, _reviewerId);
        var state = await service.UnfollowAsync(_viewerId, _reviewerId);

        Assert.False(state.IsFollowedByMe);
        Assert.False(await context.UserFollows.AnyAsync());
    }

    [Fact]
    public async Task Follow_IsOneDirectional()
    {
        await using var context = CreateContext();

        await CreateFollowService(context).FollowAsync(_viewerId, _reviewerId);

        var follow = await context.UserFollows.SingleAsync();
        Assert.Equal(_viewerId, follow.FollowerId);
        Assert.Equal(_reviewerId, follow.FolloweeId);
    }

    [Fact]
    public async Task Follow_RejectsSelf()
    {
        await using var context = CreateContext();

        var error = await Assert.ThrowsAsync<SocialException>(() =>
            CreateFollowService(context).FollowAsync(_viewerId, _viewerId));

        Assert.Equal("cannot_follow_self", error.Code);
        Assert.Equal(SocialStatusCodes.BadRequest, error.StatusCode);
    }

    [Fact]
    public async Task Follow_RejectsUnknownUser()
    {
        await using var context = CreateContext();

        var error = await Assert.ThrowsAsync<SocialException>(() =>
            CreateFollowService(context).FollowAsync(_viewerId, Guid.NewGuid()));

        Assert.Equal("not_found", error.Code);
        Assert.Equal(SocialStatusCodes.NotFound, error.StatusCode);
    }

    // ── Feed content ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Feed_IsEmpty_WithoutFollows()
    {
        await SeedReviewsAsync(_reviewerId, 2);
        await using var context = CreateContext();

        var feed = await CreateFeedService(context).GetAsync(_viewerId, new FeedQuery());

        Assert.Empty(feed.Items);
        Assert.Null(feed.NextCursor);
    }

    [Fact]
    public async Task Feed_ContainsOnlyFollowedUsers_NewestFirst_WithRestaurant()
    {
        var ids = await SeedReviewsAsync(_reviewerId, 2);
        await SeedReviewsAsync(_strangerId, 2);
        await using var context = CreateContext();
        await CreateFollowService(context).FollowAsync(_viewerId, _reviewerId);

        var feed = await CreateFeedService(context).GetAsync(_viewerId, new FeedQuery());

        Assert.Equal([ids[1], ids[0]], feed.Items.Select(item => item.Id));
        Assert.All(feed.Items, item =>
        {
            Assert.Equal(_reviewerId, item.Reviewer.Id);
            Assert.Equal(_restaurantId, item.Restaurant.Id);
            Assert.Equal(DateTimeKind.Utc, item.CreatedAt.Kind);
        });
    }

    [Fact]
    public async Task Feed_ExcludesTheViewersOwnReviews()
    {
        await SeedReviewsAsync(_viewerId, 2);
        var followed = await SeedReviewsAsync(_reviewerId, 1);
        await using (var seed = CreateContext())
        {
            // A stored self-follow can't exist on SQL Server; this proves the feed excludes it anyway.
            seed.UserFollows.Add(new UserFollow { FollowerId = _viewerId, FolloweeId = _viewerId, CreatedAt = Now });
            seed.UserFollows.Add(new UserFollow { FollowerId = _viewerId, FolloweeId = _reviewerId, CreatedAt = Now });
            await seed.SaveChangesAsync();
        }

        await using var context = CreateContext();
        var feed = await CreateFeedService(context).GetAsync(_viewerId, new FeedQuery());

        Assert.Equal(followed, feed.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task Feed_ExcludesHiddenReviews_AndInactiveRestaurants()
    {
        var visible = await SeedReviewsAsync(_reviewerId, 1);
        await SeedReviewsAsync(_reviewerId, 1, isVerified: false);
        await SeedReviewsAsync(_reviewerId, 1, restaurantId: _inactiveRestaurantId);
        await using var context = CreateContext();
        await CreateFollowService(context).FollowAsync(_viewerId, _reviewerId);

        var feed = await CreateFeedService(context).GetAsync(_viewerId, new FeedQuery());

        Assert.Equal(visible, feed.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task Unfollow_RemovesReviewsFromTheFeed()
    {
        await SeedReviewsAsync(_reviewerId, 2);
        await using var context = CreateContext();
        var follows = CreateFollowService(context);
        await follows.FollowAsync(_viewerId, _reviewerId);
        await follows.UnfollowAsync(_viewerId, _reviewerId);

        var feed = await CreateFeedService(context).GetAsync(_viewerId, new FeedQuery());

        Assert.Empty(feed.Items);
    }

    [Fact]
    public async Task Feed_ShowsCountsAndLikeStatus()
    {
        var ids = await SeedReviewsAsync(_reviewerId, 1);
        await using (var seed = CreateContext())
        {
            seed.ReviewLikes.Add(new ReviewLike { ReviewId = ids[0], UserId = _viewerId, CreatedAt = Now });
            seed.ReviewLikes.Add(new ReviewLike { ReviewId = ids[0], UserId = _strangerId, CreatedAt = Now });
            seed.ReviewComments.Add(new ReviewComment
            {
                Id = Guid.NewGuid(),
                ReviewId = ids[0],
                UserId = _strangerId,
                Text = "تعليق",
                CreatedAt = Now,
            });
            await seed.SaveChangesAsync();
        }

        await using var context = CreateContext();
        await CreateFollowService(context).FollowAsync(_viewerId, _reviewerId);

        var item = (await CreateFeedService(context).GetAsync(_viewerId, new FeedQuery())).Items.Single();

        Assert.Equal(2, item.LikeCount);
        Assert.Equal(1, item.CommentCount);
        Assert.True(item.IsLikedByMe);
    }

    // ── Cursor paging ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Cursor_WalksAllItems_WithNoRepeatOrGap()
    {
        var ids = await SeedReviewsAsync(_reviewerId, 7);
        await using var context = CreateContext();
        await CreateFollowService(context).FollowAsync(_viewerId, _reviewerId);
        var service = CreateFeedService(context);

        var seen = new List<Guid>();
        string? cursor = null;
        do
        {
            var page = await service.GetAsync(_viewerId, new FeedQuery(cursor, Limit: 3));
            seen.AddRange(page.Items.Select(item => item.Id));
            cursor = page.NextCursor;
        }
        while (cursor is not null);

        Assert.Equal(Enumerable.Reverse(ids), seen);
    }

    [Fact]
    public async Task Cursor_HandlesTiesOnCreatedAt()
    {
        var ids = await SeedReviewsAsync(_reviewerId, 5, sameTimestamp: true);
        await using var context = CreateContext();
        await CreateFollowService(context).FollowAsync(_viewerId, _reviewerId);
        var service = CreateFeedService(context);

        var first = await service.GetAsync(_viewerId, new FeedQuery(Limit: 2));
        var second = await service.GetAsync(_viewerId, new FeedQuery(first.NextCursor, Limit: 2));
        var third = await service.GetAsync(_viewerId, new FeedQuery(second.NextCursor, Limit: 2));

        var seen = first.Items.Concat(second.Items).Concat(third.Items).Select(item => item.Id).ToList();
        Assert.Equal(5, seen.Distinct().Count());
        Assert.Equal(ids.Order(), seen.Order());
        Assert.Null(third.NextCursor);
    }

    [Fact]
    public async Task Cursor_NewReviewBetweenPages_CausesNoDuplicates()
    {
        await SeedReviewsAsync(_reviewerId, 4);
        await using var context = CreateContext();
        await CreateFollowService(context).FollowAsync(_viewerId, _reviewerId);
        var service = CreateFeedService(context);

        var first = await service.GetAsync(_viewerId, new FeedQuery(Limit: 2));
        await SeedReviewsAsync(_reviewerId, 1, minutesOffset: 100);
        var second = await service.GetAsync(_viewerId, new FeedQuery(first.NextCursor, Limit: 2));

        var seen = first.Items.Concat(second.Items).Select(item => item.Id).ToList();
        Assert.Equal(4, seen.Distinct().Count());
    }

    [Fact]
    public async Task LastPage_HasNoCursor_WhenItemsFitExactly()
    {
        await SeedReviewsAsync(_reviewerId, 2);
        await using var context = CreateContext();
        await CreateFollowService(context).FollowAsync(_viewerId, _reviewerId);

        var feed = await CreateFeedService(context).GetAsync(_viewerId, new FeedQuery(Limit: 2));

        Assert.Equal(2, feed.Items.Count);
        Assert.Null(feed.NextCursor);
    }

    [Theory]
    [InlineData("not-a-cursor")]
    [InlineData("%%%")]
    [InlineData("MTIz")]
    public async Task BadCursor_IsAValidationError(string cursor)
    {
        await using var context = CreateContext();

        var error = await Assert.ThrowsAsync<SocialException>(() =>
            CreateFeedService(context).GetAsync(_viewerId, new FeedQuery(cursor)));

        Assert.Equal("validation_error", error.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public async Task BadLimit_IsAValidationError(int limit)
    {
        await using var context = CreateContext();

        var error = await Assert.ThrowsAsync<SocialException>(() =>
            CreateFeedService(context).GetAsync(_viewerId, new FeedQuery(Limit: limit)));

        Assert.Equal("validation_error", error.Code);
    }

    // ── Review detail ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ReviewDetail_ShowsFollowState_PerViewer()
    {
        var ids = await SeedReviewsAsync(_reviewerId, 1);
        await using var context = CreateContext();
        await CreateFollowService(context).FollowAsync(_viewerId, _reviewerId);
        var reviews = new ReviewService(new ReviewRepository(context), new RestaurantRepository(context));

        Assert.True((await reviews.GetByIdAsync(ids[0], _viewerId)).IsReviewerFollowedByMe);
        Assert.False((await reviews.GetByIdAsync(ids[0], _strangerId)).IsReviewerFollowedByMe);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private UserFollowService CreateFollowService(AppDbContext context) =>
        new(new UserFollowRepository(context), _clock.Object);

    private static FeedService CreateFeedService(AppDbContext context) => new(new ReviewRepository(context));

    private AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_databaseName).Options);

    /// <summary>Adds reviews oldest first and returns their ids in that order.</summary>
    private async Task<List<Guid>> SeedReviewsAsync(
        Guid userId,
        int count,
        bool isVerified = true,
        Guid? restaurantId = null,
        bool sameTimestamp = false,
        int minutesOffset = 0)
    {
        await using var context = CreateContext();
        var reviews = Enumerable.Range(0, count)
            .Select(index => new Review
            {
                Id = Guid.NewGuid(),
                RestaurantId = restaurantId ?? _restaurantId,
                UserId = userId,
                Rating = 5,
                FoodRating = 4,
                ServiceRating = 4,
                ValueRating = 4,
                AtmosphereRating = 4,
                Text = "تقييم",
                IsVerified = isVerified,
                VisitTokenId = isVerified ? Guid.NewGuid() : null,
                CreatedAt = Now.AddMinutes(minutesOffset + (sameTimestamp ? 0 : index)),
            })
            .ToList();

        context.Reviews.AddRange(reviews);
        await context.SaveChangesAsync();
        return reviews.Select(review => review.Id).ToList();
    }

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
}
