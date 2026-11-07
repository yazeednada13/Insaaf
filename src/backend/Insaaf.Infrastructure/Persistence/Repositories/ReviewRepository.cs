using Insaaf.Domain.Entities;
using Insaaf.Domain.Repositories;
using Insaaf.Domain.Repositories.Projections;
using Insaaf.Domain.Rules;
using Microsoft.EntityFrameworkCore;

namespace Insaaf.Infrastructure.Persistence.Repositories;

public sealed class ReviewRepository : IReviewRepository
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 50;

    private readonly AppDbContext _dbContext;

    public ReviewRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResult<ReviewListItemProjection>> ListByRestaurantAsync(
        ReviewSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var page = criteria.Page < DefaultPage ? DefaultPage : criteria.Page;
        var pageSize = criteria.PageSize < 1
            ? DefaultPageSize
            : Math.Min(criteria.PageSize, MaxPageSize);

        var reviews = PublicReviews().Where(review => review.RestaurantId == criteria.RestaurantId);

        var totalCount = await reviews.CountAsync(cancellationToken);

        // Reviewer details come from the same query via a join (no per-row lookups).
        var items = await ApplySort(reviews, criteria.Sort)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Join(
                _dbContext.Users,
                review => review.UserId,
                user => user.Id,
                (review, user) => new ReviewListItemProjection(
                    review.Id,
                    review.Rating,
                    review.Text,
                    review.CreatedAt,
                    review.IsVerified,
                    user.Id,
                    user.FullName,
                    user.UserName,
                    _dbContext.ReviewLikes.Count(like => like.ReviewId == review.Id),
                    _dbContext.ReviewComments.Count(comment => comment.ReviewId == review.Id),
                    _dbContext.ReviewLikes.Any(like => like.ReviewId == review.Id && like.UserId == criteria.ViewerUserId),
                    !_dbContext.AdminUserIds().Contains(user.Id)))
            .ToListAsync(cancellationToken);

        return new PagedResult<ReviewListItemProjection>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    public Task<ReviewDetailProjection?> GetPublicDetailAsync(
        Guid reviewId,
        Guid viewerUserId,
        CancellationToken cancellationToken = default) =>
        PublicReviewsOfActiveRestaurants()
            .Where(review => review.Id == reviewId)
            .Join(
                _dbContext.Users,
                review => review.UserId,
                user => user.Id,
                (review, user) => new ReviewDetailProjection(
                    review.Id,
                    review.Rating,
                    review.FoodRating,
                    review.ServiceRating,
                    review.ValueRating,
                    review.AtmosphereRating,
                    review.Text,
                    review.CreatedAt,
                    review.IsVerified,
                    user.Id,
                    user.FullName,
                    user.UserName,
                    review.RestaurantId,
                    review.Restaurant.Name,
                    review.Restaurant.City,
                    review.Restaurant.Address,
                    _dbContext.ReviewLikes.Count(like => like.ReviewId == review.Id),
                    _dbContext.ReviewComments.Count(comment => comment.ReviewId == review.Id),
                    _dbContext.ReviewLikes.Any(like => like.ReviewId == review.Id && like.UserId == viewerUserId),
                    _dbContext.UserFollows.Any(follow =>
                        follow.FollowerId == viewerUserId && follow.FolloweeId == review.UserId),
                    !_dbContext.AdminUserIds().Contains(user.Id)))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<IReadOnlyList<FeedItemProjection>> ListByUserAsync(
        Guid userId,
        Guid viewerUserId,
        FeedCursor? after,
        int take,
        CancellationToken cancellationToken = default) =>
        ReadStreamAsync(
            PublicReviewsOfActiveRestaurants().Where(review => review.UserId == userId),
            viewerUserId,
            after,
            take,
            cancellationToken);

    public Task<IReadOnlyList<FeedItemProjection>> ListFollowingFeedAsync(
        Guid viewerUserId,
        FeedCursor? after,
        int take,
        CancellationToken cancellationToken = default)
    {
        var followedUserIds = _dbContext.UserFollows
            .Where(follow => follow.FollowerId == viewerUserId)
            .Select(follow => follow.FolloweeId);

        // Starts from the shared visibility query: the feed has no visibility rule of its own.
        var reviews = PublicReviewsOfActiveRestaurants()
            .Where(review => review.UserId != viewerUserId && followedUserIds.Contains(review.UserId));

        return ReadStreamAsync(reviews, viewerUserId, after, take, cancellationToken);
    }

    private async Task<IReadOnlyList<FeedItemProjection>> ReadStreamAsync(
        IQueryable<Review> reviews,
        Guid viewerUserId,
        FeedCursor? after,
        int take,
        CancellationToken cancellationToken)
    {
        if (after is not null)
        {
            reviews = reviews.Where(review =>
                review.CreatedAt < after.CreatedAt
                || (review.CreatedAt == after.CreatedAt && review.Id < after.Id));
        }

        // Reviewer, restaurant and counts all come from this one statement (no per-row lookups).
        return await reviews
            .Join(
                _dbContext.Users,
                review => review.UserId,
                user => user.Id,
                (review, user) => new { Review = review, User = user })
            .OrderByDescending(row => row.Review.CreatedAt)
            .ThenByDescending(row => row.Review.Id)
            .Take(take)
            .Select(row => new FeedItemProjection(
                row.Review.Id,
                row.Review.Rating,
                row.Review.Text,
                row.Review.CreatedAt,
                row.Review.IsVerified,
                row.User.Id,
                row.User.FullName,
                row.User.UserName,
                row.Review.RestaurantId,
                row.Review.Restaurant.Name,
                row.Review.Restaurant.City,
                row.Review.Restaurant.Address,
                _dbContext.ReviewLikes.Count(like => like.ReviewId == row.Review.Id),
                _dbContext.ReviewComments.Count(comment => comment.ReviewId == row.Review.Id),
                _dbContext.ReviewLikes.Any(like => like.ReviewId == row.Review.Id && like.UserId == viewerUserId),
                !_dbContext.AdminUserIds().Contains(row.User.Id)))
            .ToListAsync(cancellationToken);
    }

    public Task<bool> IsPublicAsync(Guid reviewId, CancellationToken cancellationToken = default) =>
        PublicReviewsOfActiveRestaurants().AnyAsync(review => review.Id == reviewId, cancellationToken);

    private IQueryable<Review> PublicReviewsOfActiveRestaurants() =>
        _dbContext.PublicReviewsOfActiveRestaurants();

    private IQueryable<Review> PublicReviews() =>
        _dbContext.Reviews
            .AsNoTracking()
            .Where(ReviewVisibility.IsPublic);

    // Every order ends with Id so paging is stable when ratings or timestamps tie.
    private static IOrderedQueryable<Review> ApplySort(IQueryable<Review> reviews, ReviewSort sort) =>
        sort switch
        {
            ReviewSort.Highest => reviews
                .OrderByDescending(review => review.Rating)
                .ThenByDescending(review => review.CreatedAt)
                .ThenBy(review => review.Id),
            ReviewSort.Lowest => reviews
                .OrderBy(review => review.Rating)
                .ThenByDescending(review => review.CreatedAt)
                .ThenBy(review => review.Id),
            _ => reviews
                .OrderByDescending(review => review.CreatedAt)
                .ThenBy(review => review.Id),
        };
}
