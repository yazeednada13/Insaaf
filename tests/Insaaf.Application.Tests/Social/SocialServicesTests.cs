using Insaaf.Application.Auth.Abstractions;
using Insaaf.Application.Reviews;
using Insaaf.Application.Reviews.Dtos;
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

/// <summary>Likes, comments and review detail on the real repositories (InMemory).</summary>
public sealed class SocialServicesTests
{
    private static readonly DateTime Now = new(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _databaseName = $"social-{Guid.NewGuid():N}";
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _otherUserId = Guid.NewGuid();
    private readonly Guid _restaurantId = Guid.NewGuid();
    private readonly Guid _inactiveRestaurantId = Guid.NewGuid();
    private readonly Guid _reviewId = Guid.NewGuid();
    private readonly Guid _otherReviewId = Guid.NewGuid();
    private readonly Guid _hiddenReviewId = Guid.NewGuid();
    private readonly Guid _inactiveRestaurantReviewId = Guid.NewGuid();

    public SocialServicesTests()
    {
        _clock.Setup(clock => clock.UtcNow).Returns(Now);
        using var context = CreateContext();
        context.Users.AddRange(
            new ApplicationUser { Id = _userId, UserName = "user", FullName = "User One", Email = "u@example.com" },
            new ApplicationUser { Id = _otherUserId, UserName = "other", FullName = "User Two", Email = "o@example.com" });
        context.Restaurants.AddRange(
            CreateRestaurant(_restaurantId, isActive: true),
            CreateRestaurant(_inactiveRestaurantId, isActive: false));
        context.Reviews.AddRange(
            CreateReview(_reviewId, _restaurantId, isVerified: true),
            CreateReview(_otherReviewId, _restaurantId, isVerified: true),
            CreateReview(_hiddenReviewId, _restaurantId, isVerified: false),
            CreateReview(_inactiveRestaurantReviewId, _inactiveRestaurantId, isVerified: true));
        context.SaveChanges();
    }

    // ── Likes ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Like_Twice_KeepsOneRow()
    {
        await using var context = CreateContext();
        var service = CreateLikeService(context);

        await service.LikeAsync(_reviewId, _userId);
        var state = await service.LikeAsync(_reviewId, _userId);

        Assert.Equal(1, state.LikeCount);
        Assert.True(state.IsLikedByMe);
        Assert.Equal(1, await context.ReviewLikes.CountAsync());
    }

    [Fact]
    public async Task Like_FromTwoContexts_KeepsOneRow()
    {
        await using var first = CreateContext();
        await using var second = CreateContext();

        await CreateLikeService(first).LikeAsync(_reviewId, _userId);
        await CreateLikeService(second).LikeAsync(_reviewId, _userId);

        await using var check = CreateContext();
        Assert.Equal(1, await check.ReviewLikes.CountAsync());
    }

    [Fact]
    public async Task Unlike_WhenNotLiked_ChangesNothing()
    {
        await using var context = CreateContext();
        var service = CreateLikeService(context);

        await service.LikeAsync(_reviewId, _otherUserId);
        var state = await service.UnlikeAsync(_reviewId, _userId);

        Assert.Equal(1, state.LikeCount);
        Assert.False(state.IsLikedByMe);
    }

    [Fact]
    public async Task Unlike_RemovesOnlyTheUsersLike()
    {
        await using var context = CreateContext();
        var service = CreateLikeService(context);
        await service.LikeAsync(_reviewId, _userId);
        await service.LikeAsync(_reviewId, _otherUserId);

        var state = await service.UnlikeAsync(_reviewId, _userId);

        Assert.Equal(1, state.LikeCount);
        Assert.Equal(_otherUserId, (await context.ReviewLikes.SingleAsync()).UserId);
    }

    [Fact]
    public async Task Users_CanLikeTheirOwnReview()
    {
        await using var context = CreateContext();

        var state = await CreateLikeService(context).LikeAsync(_reviewId, _userId);

        Assert.True(state.IsLikedByMe);
    }

    [Fact]
    public async Task Likes_AreRejected_ForHiddenUnknownOrInactiveRestaurantReviews()
    {
        await using var context = CreateContext();
        var service = CreateLikeService(context);

        foreach (var reviewId in new[] { _hiddenReviewId, Guid.NewGuid(), _inactiveRestaurantReviewId })
        {
            var error = await Assert.ThrowsAsync<SocialException>(() => service.LikeAsync(reviewId, _userId));
            Assert.Equal("not_found", error.Code);
            Assert.Equal(SocialStatusCodes.NotFound, error.StatusCode);
        }

        Assert.False(await context.ReviewLikes.AnyAsync());
    }

    // ── Comments ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Create_RejectsEmptyText(string? text)
    {
        await using var context = CreateContext();

        var error = await Assert.ThrowsAsync<SocialException>(() =>
            CreateCommentService(context).CreateAsync(_reviewId, _userId, new CreateReviewCommentRequest(text)));

        Assert.Equal("validation_error", error.Code);
        Assert.False(await context.ReviewComments.AnyAsync());
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(500, true)]
    [InlineData(501, false)]
    public async Task Create_EnforcesLengthBoundaries(int length, bool valid)
    {
        await using var context = CreateContext();
        var request = new CreateReviewCommentRequest(new string('ن', length));
        var service = CreateCommentService(context);

        if (valid)
        {
            var created = await service.CreateAsync(_reviewId, _userId, request);
            Assert.Equal(length, created.Text.Length);
        }
        else
        {
            var error = await Assert.ThrowsAsync<SocialException>(() => service.CreateAsync(_reviewId, _userId, request));
            Assert.Equal("validation_error", error.Code);
        }
    }

    [Fact]
    public async Task Create_TrimsText_AndReturnsAuthor()
    {
        await using var context = CreateContext();

        var created = await CreateCommentService(context)
            .CreateAsync(_reviewId, _userId, new CreateReviewCommentRequest("  تعليق  "));

        Assert.Equal("تعليق", created.Text);
        Assert.Equal(_userId, created.Author.Id);
        Assert.Equal("User One", created.Author.FullName);
        Assert.True(created.IsMine);
        Assert.Equal(DateTimeKind.Utc, created.CreatedAt.Kind);
    }

    [Fact]
    public async Task List_IsOldestFirst_Paged_AndMarksOwnComments()
    {
        await using (var seed = CreateContext())
        {
            seed.ReviewComments.AddRange(
                CreateComment(_reviewId, _otherUserId, "ثالث", Now.AddMinutes(2)),
                CreateComment(_reviewId, _userId, "أول", Now),
                CreateComment(_reviewId, _otherUserId, "ثاني", Now.AddMinutes(1)),
                CreateComment(_otherReviewId, _userId, "مراجعة أخرى", Now));
            await seed.SaveChangesAsync();
        }

        await using var context = CreateContext();
        var service = CreateCommentService(context);

        var first = await service.ListAsync(_reviewId, _userId, new ReviewCommentsQuery(Page: 1, PageSize: 2));
        var second = await service.ListAsync(_reviewId, _userId, new ReviewCommentsQuery(Page: 2, PageSize: 2));

        Assert.Equal(3, first.TotalCount);
        Assert.Equal(["أول", "ثاني"], first.Items.Select(item => item.Text));
        Assert.True(first.Items[0].IsMine);
        Assert.False(first.Items[1].IsMine);
        Assert.Equal("ثالث", Assert.Single(second.Items).Text);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 51)]
    public async Task List_RejectsBadPaging(int page, int pageSize)
    {
        await using var context = CreateContext();

        var error = await Assert.ThrowsAsync<SocialException>(() =>
            CreateCommentService(context).ListAsync(_reviewId, _userId, new ReviewCommentsQuery(page, pageSize)));

        Assert.Equal("validation_error", error.Code);
    }

    [Fact]
    public async Task Delete_OwnComment_RemovesIt_AndReturnsTheCount()
    {
        await using var context = CreateContext();
        var service = CreateCommentService(context);
        var mine = await service.CreateAsync(_reviewId, _userId, new CreateReviewCommentRequest("لي"));
        await service.CreateAsync(_reviewId, _otherUserId, new CreateReviewCommentRequest("لغيري"));

        var deleted = await service.DeleteAsync(_reviewId, mine.Id, _userId);

        Assert.Equal(_reviewId, deleted.ReviewId);
        Assert.Equal(1, deleted.CommentCount);
        Assert.False(await context.ReviewComments.AnyAsync(comment => comment.Id == mine.Id));
    }

    [Fact]
    public async Task Delete_OthersComment_IsForbidden_AndKeepsIt()
    {
        await using var context = CreateContext();
        var service = CreateCommentService(context);
        var theirs = await service.CreateAsync(_reviewId, _otherUserId, new CreateReviewCommentRequest("لغيري"));

        var error = await Assert.ThrowsAsync<SocialException>(() => service.DeleteAsync(_reviewId, theirs.Id, _userId));

        Assert.Equal("comment_forbidden", error.Code);
        Assert.Equal(SocialStatusCodes.Forbidden, error.StatusCode);
        Assert.True(await context.ReviewComments.AnyAsync(comment => comment.Id == theirs.Id));
    }

    [Fact]
    public async Task Delete_ReviewAuthorHasNoPowerOverComments()
    {
        await using var seed = CreateContext();
        var service = CreateCommentService(seed);
        // _reviewId was written by _userId; a comment by someone else stays theirs.
        var comment = await service.CreateAsync(_reviewId, _otherUserId, new CreateReviewCommentRequest("تعليق"));

        await Assert.ThrowsAsync<SocialException>(() => service.DeleteAsync(_reviewId, comment.Id, _userId));
    }

    [Fact]
    public async Task Delete_UnderTheWrongReview_OrUnknownComment_IsNotFound()
    {
        await using var context = CreateContext();
        var service = CreateCommentService(context);
        var comment = await service.CreateAsync(_reviewId, _userId, new CreateReviewCommentRequest("تعليق"));

        var wrongReview = await Assert.ThrowsAsync<SocialException>(() =>
            service.DeleteAsync(_otherReviewId, comment.Id, _userId));
        var unknown = await Assert.ThrowsAsync<SocialException>(() =>
            service.DeleteAsync(_reviewId, Guid.NewGuid(), _userId));

        Assert.Equal("not_found", wrongReview.Code);
        Assert.Equal("not_found", unknown.Code);
        Assert.True(await context.ReviewComments.AnyAsync(item => item.Id == comment.Id));
    }

    [Fact]
    public async Task Comments_AreRejected_ForHiddenReviews()
    {
        await using var context = CreateContext();

        var error = await Assert.ThrowsAsync<SocialException>(() =>
            CreateCommentService(context).CreateAsync(_hiddenReviewId, _userId, new CreateReviewCommentRequest("تعليق")));

        Assert.Equal("not_found", error.Code);
    }

    // ── Reads: counts and status ──────────────────────────────────────────────

    [Fact]
    public async Task ReviewList_ShowsCounts_AndPerUserLikeStatus()
    {
        await using var context = CreateContext();
        await CreateLikeService(context).LikeAsync(_reviewId, _userId);
        await CreateLikeService(context).LikeAsync(_reviewId, _otherUserId);
        await CreateCommentService(context).CreateAsync(_reviewId, _userId, new CreateReviewCommentRequest("تعليق"));
        await CreateLikeService(context).LikeAsync(_otherReviewId, _otherUserId);

        var forUser = await CreateReviewService(context)
            .ListForRestaurantAsync(_restaurantId, new RestaurantReviewsQuery(), _userId);
        var forOther = await CreateReviewService(context)
            .ListForRestaurantAsync(_restaurantId, new RestaurantReviewsQuery(), _otherUserId);

        var review = forUser.Items.Single(item => item.Id == _reviewId);
        Assert.Equal(2, review.LikeCount);
        Assert.Equal(1, review.CommentCount);
        Assert.True(review.IsLikedByMe);
        Assert.False(forUser.Items.Single(item => item.Id == _otherReviewId).IsLikedByMe);
        Assert.True(forOther.Items.Single(item => item.Id == _otherReviewId).IsLikedByMe);
    }

    [Fact]
    public async Task ReviewDetail_ReturnsSubRatings_RestaurantAndSocialState()
    {
        await using var context = CreateContext();
        await CreateLikeService(context).LikeAsync(_reviewId, _userId);

        var detail = await CreateReviewService(context).GetByIdAsync(_reviewId, _userId);

        Assert.Equal(_reviewId, detail.Id);
        Assert.Equal(4, detail.FoodRating);
        Assert.Equal(3, detail.ServiceRating);
        Assert.Equal(_restaurantId, detail.Restaurant.Id);
        Assert.Equal(1, detail.LikeCount);
        Assert.True(detail.IsLikedByMe);
        Assert.Equal(DateTimeKind.Utc, detail.CreatedAt.Kind);
    }

    [Fact]
    public async Task ReviewDetail_IsNotFound_ForHiddenOrInactiveRestaurantReviews()
    {
        await using var context = CreateContext();
        var service = CreateReviewService(context);

        foreach (var reviewId in new[] { _hiddenReviewId, _inactiveRestaurantReviewId, Guid.NewGuid() })
        {
            var error = await Assert.ThrowsAsync<SocialException>(() => service.GetByIdAsync(reviewId, _userId));
            Assert.Equal("not_found", error.Code);
        }
    }

    // ── Rating isolation ──────────────────────────────────────────────────────

    [Fact]
    public async Task LikesAndComments_NeverChangeRatings()
    {
        await using var context = CreateContext();
        var restaurants = new RestaurantRepository(context);

        var before = await restaurants.GetByIdAsync(_restaurantId);
        var reviewBefore = await context.Reviews.AsNoTracking().SingleAsync(review => review.Id == _reviewId);

        var likes = CreateLikeService(context);
        var comments = CreateCommentService(context);
        await likes.LikeAsync(_reviewId, _userId);
        await likes.LikeAsync(_reviewId, _otherUserId);
        var comment = await comments.CreateAsync(_reviewId, _userId, new CreateReviewCommentRequest("تعليق"));
        await comments.CreateAsync(_otherReviewId, _otherUserId, new CreateReviewCommentRequest("تعليق آخر"));
        await comments.DeleteAsync(_reviewId, comment.Id, _userId);
        await likes.UnlikeAsync(_reviewId, _otherUserId);

        var after = await restaurants.GetByIdAsync(_restaurantId);
        var reviewAfter = await context.Reviews.AsNoTracking().SingleAsync(review => review.Id == _reviewId);

        Assert.Equal(before!.AverageRating, after!.AverageRating);
        Assert.Equal(before.ReviewCount, after.ReviewCount);
        Assert.Equal(before.RatingBreakdown, after.RatingBreakdown);
        Assert.Equal(reviewBefore.Rating, reviewAfter.Rating);
        Assert.Equal(reviewBefore.FoodRating, reviewAfter.FoodRating);
        Assert.Equal(reviewBefore.Text, reviewAfter.Text);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private ReviewLikeService CreateLikeService(AppDbContext context) =>
        new(new ReviewRepository(context), new ReviewLikeRepository(context), _clock.Object);

    private ReviewCommentService CreateCommentService(AppDbContext context) =>
        new(new ReviewRepository(context), new ReviewCommentRepository(context), _clock.Object);

    private static ReviewService CreateReviewService(AppDbContext context) =>
        new(new ReviewRepository(context), new RestaurantRepository(context));

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

    private Review CreateReview(Guid id, Guid restaurantId, bool isVerified) =>
        new()
        {
            Id = id,
            RestaurantId = restaurantId,
            UserId = _userId,
            Rating = 5,
            FoodRating = 4,
            ServiceRating = 3,
            ValueRating = 4,
            AtmosphereRating = 5,
            Text = "تقييم",
            IsVerified = isVerified,
            VisitTokenId = isVerified ? Guid.NewGuid() : null,
            CreatedAt = Now,
        };

    private static ReviewComment CreateComment(Guid reviewId, Guid userId, string text, DateTime createdAt) =>
        new()
        {
            Id = Guid.NewGuid(),
            ReviewId = reviewId,
            UserId = userId,
            Text = text,
            CreatedAt = createdAt,
        };
}
