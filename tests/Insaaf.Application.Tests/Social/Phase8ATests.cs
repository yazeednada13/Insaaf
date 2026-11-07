using Insaaf.Application.Auth.Abstractions;
using Insaaf.Application.Social;
using Insaaf.Application.Social.Dtos;
using Insaaf.Domain.Constants;
using Insaaf.Domain.Entities;
using Insaaf.Domain.Enums;
using Insaaf.Domain.Repositories.Projections;
using Insaaf.Infrastructure.Identity;
using Insaaf.Infrastructure.Persistence;
using Insaaf.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Insaaf.Application.Tests.Social;

/// <summary>Notifications, Home ranking and Taste Match on the real repositories (InMemory).</summary>
public sealed class Phase8ATests
{
    private static readonly DateTime Now = new(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _databaseName = $"phase8a-{Guid.NewGuid():N}";
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Guid _authorId = Guid.NewGuid();
    private readonly Guid _otherId = Guid.NewGuid();
    private readonly Guid _thirdId = Guid.NewGuid();
    private readonly Guid _adminId = Guid.NewGuid();
    private readonly Guid _burgerId = Guid.NewGuid();
    private readonly Guid _pizzaId = Guid.NewGuid();
    private readonly Guid _burgerOne = Guid.NewGuid();
    private readonly Guid _burgerTwo = Guid.NewGuid();
    private readonly Guid _pizzaOne = Guid.NewGuid();
    private readonly Guid _inactive = Guid.NewGuid();
    private readonly Guid _reviewId = Guid.NewGuid();

    public Phase8ATests()
    {
        _clock.Setup(clock => clock.UtcNow).Returns(Now);
        var adminRoleId = Guid.NewGuid();
        using var context = CreateContext();
        context.Users.AddRange(
            new ApplicationUser { Id = _authorId, UserName = "author", FullName = "كاتب", Email = "a@example.com" },
            new ApplicationUser { Id = _otherId, UserName = "other", FullName = "أحمد", Email = "o@example.com" },
            new ApplicationUser { Id = _thirdId, UserName = "third", FullName = "سارة", Email = "t@example.com" },
            new ApplicationUser { Id = _adminId, UserName = "admin", FullName = "الإدارة", Email = "ad@example.com" });
        context.Roles.Add(new IdentityRole<Guid> { Id = adminRoleId, Name = Roles.Admin, NormalizedName = "ADMIN" });
        context.UserRoles.Add(new IdentityUserRole<Guid> { UserId = _adminId, RoleId = adminRoleId });
        context.Categories.AddRange(
            new Category { Id = _burgerId, Name = "برجر", Slug = "burger" },
            new Category { Id = _pizzaId, Name = "بيتزا", Slug = "pizza" });
        context.Restaurants.AddRange(
            CreateRestaurant(_burgerOne, isActive: true),
            CreateRestaurant(_burgerTwo, isActive: true),
            CreateRestaurant(_pizzaOne, isActive: true),
            CreateRestaurant(_inactive, isActive: false));
        context.RestaurantCategories.AddRange(
            new RestaurantCategory { RestaurantId = _burgerOne, CategoryId = _burgerId },
            new RestaurantCategory { RestaurantId = _burgerTwo, CategoryId = _burgerId },
            new RestaurantCategory { RestaurantId = _pizzaOne, CategoryId = _pizzaId },
            new RestaurantCategory { RestaurantId = _inactive, CategoryId = _burgerId });
        context.Reviews.Add(CreateReview(_reviewId, _authorId, _burgerOne, rating: 5, daysAgo: 1));
        context.SaveChanges();
    }

    // ── Notification triggers ─────────────────────────────────────────────────

    [Fact]
    public async Task Like_NotifiesTheAuthor_OncePerActor_AndUnlikeRemovesIt()
    {
        await using var context = CreateContext();
        var likes = CreateLikeService(context);

        await likes.LikeAsync(_reviewId, _otherId);
        await likes.UnlikeAsync(_reviewId, _otherId);
        await likes.LikeAsync(_reviewId, _otherId);
        await likes.LikeAsync(_reviewId, _otherId);

        var notification = Assert.Single(await context.Notifications.ToListAsync());
        Assert.Equal(_authorId, notification.RecipientId);
        Assert.Equal(_otherId, notification.ActorId);
        Assert.Equal(NotificationType.Like, notification.Type);

        await likes.UnlikeAsync(_reviewId, _otherId);
        Assert.False(await context.Notifications.AnyAsync());
    }

    [Fact]
    public async Task OwnActions_NeverNotify()
    {
        await using var context = CreateContext();

        await CreateLikeService(context).LikeAsync(_reviewId, _authorId);
        await CreateCommentService(context).CreateAsync(_reviewId, _authorId, new CreateReviewCommentRequest("تعليقي"));

        Assert.False(await context.Notifications.AnyAsync());
    }

    [Fact]
    public async Task Comment_Notifies_AndDeletingItRemovesIt()
    {
        await using var context = CreateContext();
        var comments = CreateCommentService(context);

        var comment = await comments.CreateAsync(_reviewId, _otherId, new CreateReviewCommentRequest("تعليق"));

        var notification = Assert.Single(await context.Notifications.ToListAsync());
        Assert.Equal(NotificationType.Comment, notification.Type);
        Assert.Equal(comment.Id, notification.CommentId);

        await comments.DeleteAsync(_reviewId, comment.Id, _otherId);
        Assert.False(await context.Notifications.AnyAsync());
    }

    [Fact]
    public async Task Follow_Notifies_Once_AndUnfollowRemovesIt()
    {
        await using var context = CreateContext();
        var follows = CreateFollowService(context);

        await follows.FollowAsync(_otherId, _authorId);
        await follows.FollowAsync(_otherId, _authorId);

        var notification = Assert.Single(await context.Notifications.ToListAsync());
        Assert.Equal(NotificationType.Follow, notification.Type);
        Assert.Null(notification.ReviewId);

        await follows.UnfollowAsync(_otherId, _authorId);
        Assert.False(await context.Notifications.AnyAsync());
    }

    // ── Reading notifications ─────────────────────────────────────────────────

    [Fact]
    public async Task List_IsNewestFirst_Paged_WithActorAndType()
    {
        await using (var seed = CreateContext())
        {
            seed.Notifications.AddRange(
                CreateNotification(_authorId, _otherId, NotificationType.Follow, null, minutesAgo: 30),
                CreateNotification(_authorId, _thirdId, NotificationType.Like, _reviewId, minutesAgo: 20),
                CreateNotification(_authorId, _adminId, NotificationType.Comment, _reviewId, minutesAgo: 10));
            await seed.SaveChangesAsync();
        }

        await using var context = CreateContext();
        var service = CreateNotificationService(context);

        var first = await service.ListAsync(_authorId, new NotificationsQuery(Limit: 2));
        var second = await service.ListAsync(_authorId, new NotificationsQuery(first.NextCursor, Limit: 2));

        Assert.Equal(["comment", "like"], first.Items.Select(item => item.Type));
        Assert.False(first.Items[0].Actor.HasPublicProfile);
        Assert.Equal("سارة", first.Items[1].Actor.FullName);
        Assert.Equal("follow", Assert.Single(second.Items).Type);
        Assert.Null(second.NextCursor);
    }

    [Fact]
    public async Task UnreadCount_AndMarkAllRead_AreForTheCallerOnly()
    {
        await using (var seed = CreateContext())
        {
            seed.Notifications.AddRange(
                CreateNotification(_authorId, _otherId, NotificationType.Follow, null, minutesAgo: 5),
                CreateNotification(_authorId, _thirdId, NotificationType.Follow, null, minutesAgo: 4),
                CreateNotification(_otherId, _authorId, NotificationType.Follow, null, minutesAgo: 3));
            await seed.SaveChangesAsync();
        }

        await using var context = CreateContext();
        var service = CreateNotificationService(context);

        Assert.Equal(2, (await service.CountUnreadAsync(_authorId)).Count);

        await service.MarkAllReadAsync(_authorId);

        Assert.Equal(0, (await service.CountUnreadAsync(_authorId)).Count);
        Assert.Equal(1, (await service.CountUnreadAsync(_otherId)).Count);
        Assert.All((await service.ListAsync(_authorId, new NotificationsQuery())).Items, item => Assert.True(item.IsRead));
    }

    [Fact]
    public async Task Notifications_AboutHiddenReviews_AreNotShown()
    {
        var hidden = Guid.NewGuid();
        await using (var seed = CreateContext())
        {
            seed.Reviews.Add(CreateReview(hidden, _authorId, _inactive, rating: 4, daysAgo: 1));
            seed.Notifications.Add(CreateNotification(_authorId, _otherId, NotificationType.Like, hidden, minutesAgo: 1));
            await seed.SaveChangesAsync();
        }

        await using var context = CreateContext();
        var service = CreateNotificationService(context);

        Assert.Empty((await service.ListAsync(_authorId, new NotificationsQuery())).Items);
        Assert.Equal(0, (await service.CountUnreadAsync(_authorId)).Count);
    }

    // ── Home ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Suggested_UsesReviewedCategories_AndSkipsReviewedRestaurants()
    {
        await using var context = CreateContext();

        var suggested = await CreateHomeService(context).GetSuggestedAsync(_authorId, new HomeRestaurantsQuery());

        Assert.Equal([_burgerTwo], suggested.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task Suggested_FallsBackToBestRated_WithoutHistory()
    {
        await using var context = CreateContext();

        var suggested = await CreateHomeService(context).GetSuggestedAsync(_otherId, new HomeRestaurantsQuery());

        Assert.Equal(_burgerOne, suggested.Items[0].Id);
        Assert.DoesNotContain(suggested.Items, item => item.Id == _inactive);
        Assert.Equal(3, suggested.Items.Count);
    }

    [Fact]
    public async Task Trending_CountsOnlyRecentPublicReviews()
    {
        await using (var seed = CreateContext())
        {
            seed.Reviews.AddRange(
                CreateReview(Guid.NewGuid(), _otherId, _pizzaOne, rating: 3, daysAgo: 2),
                CreateReview(Guid.NewGuid(), _thirdId, _pizzaOne, rating: 3, daysAgo: 3),
                CreateReview(Guid.NewGuid(), _otherId, _burgerTwo, rating: 5, daysAgo: 60),
                CreateReview(Guid.NewGuid(), _thirdId, _burgerTwo, rating: 5, daysAgo: 61));
            await seed.SaveChangesAsync();
        }

        await using var context = CreateContext();

        var trending = await CreateHomeService(context).GetTrendingAsync(new HomeRestaurantsQuery());

        Assert.Equal([_pizzaOne, _burgerOne, _burgerTwo], trending.Items.Select(item => item.Id));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public async Task Home_RejectsBadLimits(int limit)
    {
        await using var context = CreateContext();

        await Assert.ThrowsAsync<SocialException>(() =>
            CreateHomeService(context).GetTrendingAsync(new HomeRestaurantsQuery(limit)));
    }

    // ── Taste ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Taste_NeedsThreeReviews()
    {
        await using var context = CreateContext();

        var taste = await CreateTasteService(context).GetTasteAsync(_authorId, _otherId);

        Assert.Equal(1, taste.ReviewCount);
        Assert.False(taste.HasEnoughReviews);
        Assert.Empty(taste.Categories);
    }

    [Fact]
    public async Task Taste_ShowsShares_AndLevels_FromPublicReviewsOnly()
    {
        await using (var seed = CreateContext())
        {
            seed.Reviews.AddRange(
                CreateReview(Guid.NewGuid(), _authorId, _burgerTwo, rating: 4, daysAgo: 2),
                CreateReview(Guid.NewGuid(), _authorId, _pizzaOne, rating: 3, daysAgo: 3),
                CreateReview(Guid.NewGuid(), _authorId, _inactive, rating: 1, daysAgo: 4));
            await seed.SaveChangesAsync();
        }

        await using var context = CreateContext();

        var taste = await CreateTasteService(context).GetTasteAsync(_authorId, _authorId);

        Assert.Equal(3, taste.ReviewCount);
        Assert.True(taste.HasEnoughReviews);
        Assert.Equal(("burger", 67, "very_high"), (taste.Categories[0].Slug, taste.Categories[0].Share, taste.Categories[0].Level));
        Assert.Equal(("pizza", 33, "high"), (taste.Categories[1].Slug, taste.Categories[1].Share, taste.Categories[1].Level));
    }

    [Fact]
    public void Match_ComparesAverageRatings_WeightedByTheSmallerCount()
    {
        var mine = new TasteProjection(5, [
            new CategoryTasteProjection("برجر", "burger", 4, 4.0),
            new CategoryTasteProjection("بيتزا", "pizza", 1, 5.0),
        ]);
        var theirs = new TasteProjection(4, [
            new CategoryTasteProjection("برجر", "burger", 2, 4.0),
            new CategoryTasteProjection("بيتزا", "pizza", 2, 3.0),
            new CategoryTasteProjection("قهوة", "coffee", 2, 2.0),
        ]);

        var match = UserTasteService.Match(mine, theirs);

        // Burger 100% (weight 2), pizza 50% (weight 1): (200 + 50) / 3 = 83.
        Assert.True(match.Available);
        Assert.Equal(83, match.Percentage);
        Assert.Equal([("burger", 100), ("pizza", 50)], match.Categories.Select(item => (item.Slug, item.Percentage)));
    }

    [Fact]
    public void Match_IsUnavailable_WithoutASharedCategory()
    {
        var mine = new TasteProjection(3, [new CategoryTasteProjection("برجر", "burger", 3, 4.0)]);
        var theirs = new TasteProjection(3, [new CategoryTasteProjection("بيتزا", "pizza", 3, 4.0)]);

        var match = UserTasteService.Match(mine, theirs);

        Assert.False(match.Available);
        Assert.Null(match.Percentage);
    }

    [Fact]
    public async Task Match_IsUnavailable_BelowThreeReviews()
    {
        await using var context = CreateContext();

        var match = await CreateTasteService(context).GetMatchAsync(_authorId, _otherId);

        Assert.False(match.Available);
    }

    [Fact]
    public async Task Taste_OfAnAdmin_IsNotFoundForOthers()
    {
        await using var context = CreateContext();
        var service = CreateTasteService(context);

        var taste = await Assert.ThrowsAsync<SocialException>(() => service.GetTasteAsync(_adminId, _otherId));
        var match = await Assert.ThrowsAsync<SocialException>(() => service.GetMatchAsync(_adminId, _otherId));

        Assert.Equal("not_found", taste.Code);
        Assert.Equal("not_found", match.Code);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private ReviewLikeService CreateLikeService(AppDbContext context) =>
        new(new ReviewRepository(context), new ReviewLikeRepository(context), _clock.Object, TestNotifications.Publisher(context, _clock.Object));

    private ReviewCommentService CreateCommentService(AppDbContext context) =>
        new(new ReviewRepository(context), new ReviewCommentRepository(context), _clock.Object, TestNotifications.Publisher(context, _clock.Object));

    private UserFollowService CreateFollowService(AppDbContext context) =>
        new(new UserFollowRepository(context), new UserProfileRepository(context), _clock.Object, TestNotifications.Publisher(context, _clock.Object));

    private NotificationService CreateNotificationService(AppDbContext context) =>
        new(new NotificationRepository(context, NullLogger<NotificationRepository>.Instance), _clock.Object);

    private HomeService CreateHomeService(AppDbContext context) => new(new RestaurantRepository(context), _clock.Object);

    private static UserTasteService CreateTasteService(AppDbContext context) => new(new UserProfileRepository(context));

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

    private static Review CreateReview(Guid id, Guid userId, Guid restaurantId, int rating, int daysAgo) =>
        new()
        {
            Id = id,
            RestaurantId = restaurantId,
            UserId = userId,
            Rating = rating,
            FoodRating = rating,
            ServiceRating = rating,
            ValueRating = rating,
            AtmosphereRating = rating,
            Text = "تقييم",
            IsVerified = true,
            VisitTokenId = Guid.NewGuid(),
            CreatedAt = Now.AddDays(-daysAgo),
        };

    private static Notification CreateNotification(
        Guid recipientId,
        Guid actorId,
        NotificationType type,
        Guid? reviewId,
        int minutesAgo) =>
        new()
        {
            Id = Guid.NewGuid(),
            RecipientId = recipientId,
            ActorId = actorId,
            Type = type,
            ReviewId = reviewId,
            CreatedAt = Now.AddMinutes(-minutesAgo),
        };
}
