using Insaaf.Domain.Entities;
using Insaaf.Domain.Enums;
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
                    !_dbContext.AdminUserIds().Contains(user.Id),
                    _dbContext.ReviewPhotos
                        .Where(photo => photo.ReviewId == review.Id && photo.Status == ReviewPhotoStatus.Ready)
                        .OrderBy(photo => photo.SortOrder)
                        .Select(photo => new ReviewPhotoProjection(photo.Id, photo.Width ?? 0, photo.Height ?? 0))
                        .ToList(),
                    _dbContext.SavedReviews.Any(saved => saved.ReviewId == review.Id && saved.UserId == criteria.ViewerUserId),
                    _dbContext.ReviewReplies
                        .Where(reply => reply.ReviewId == review.Id)
                        .Select(reply => new ReviewReplyProjection(reply.Text, reply.CreatedAt, reply.UpdatedAt))
                        .FirstOrDefault(),
                    _dbContext.AdminUserIds().Contains(user.Id) ? null : user.AvatarKey))
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
                    !_dbContext.AdminUserIds().Contains(user.Id),
                    _dbContext.ReviewPhotos
                        .Where(photo => photo.ReviewId == review.Id && photo.Status == ReviewPhotoStatus.Ready)
                        .OrderBy(photo => photo.SortOrder)
                        .Select(photo => new ReviewPhotoProjection(photo.Id, photo.Width ?? 0, photo.Height ?? 0))
                        .ToList(),
                    _dbContext.SavedReviews.Any(saved => saved.ReviewId == review.Id && saved.UserId == viewerUserId),
                    _dbContext.ReviewReplies
                        .Where(reply => reply.ReviewId == review.Id)
                        .Select(reply => new ReviewReplyProjection(reply.Text, reply.CreatedAt, reply.UpdatedAt))
                        .FirstOrDefault(),
                    _dbContext.AdminUserIds().Contains(user.Id) ? null : user.AvatarKey))
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

    public Task<IReadOnlyList<FeedItemProjection>> ListSavedAsync(
        Guid viewerUserId,
        FeedCursor? after,
        int take,
        CancellationToken cancellationToken = default) =>
        ReadStreamAsync(
            PublicReviewsOfActiveRestaurants().Where(review =>
                _dbContext.SavedReviews.Any(saved => saved.UserId == viewerUserId && saved.ReviewId == review.Id)),
            viewerUserId,
            after,
            take,
            cancellationToken);

    public Task<IReadOnlyList<FeedItemProjection>> ListRecentAsync(
        Guid viewerUserId,
        FeedCursor? after,
        int take,
        CancellationToken cancellationToken = default) =>
        ReadStreamAsync(PublicReviewsOfActiveRestaurants(), viewerUserId, after, take, cancellationToken);

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
                !_dbContext.AdminUserIds().Contains(row.User.Id),
                _dbContext.ReviewPhotos
                        .Where(photo => photo.ReviewId == row.Review.Id && photo.Status == ReviewPhotoStatus.Ready)
                        .OrderBy(photo => photo.SortOrder)
                        .Select(photo => new ReviewPhotoProjection(photo.Id, photo.Width ?? 0, photo.Height ?? 0))
                    .ToList(),
                _dbContext.SavedReviews.Any(saved => saved.ReviewId == row.Review.Id && saved.UserId == viewerUserId),
                _dbContext.ReviewReplies
                    .Where(reply => reply.ReviewId == row.Review.Id)
                    .Select(reply => new ReviewReplyProjection(reply.Text, reply.CreatedAt, reply.UpdatedAt))
                    .FirstOrDefault(),
                _dbContext.AdminUserIds().Contains(row.User.Id) ? null : row.User.AvatarKey))
            .ToListAsync(cancellationToken);
    }

    public Task<Guid?> GetAuthorIdAsync(Guid reviewId, CancellationToken cancellationToken = default) =>
        _dbContext.Reviews
            .AsNoTracking()
            .Where(review => review.Id == reviewId)
            .Select(review => (Guid?)review.UserId)
            .SingleOrDefaultAsync(cancellationToken);

    public Task<bool> IsPublicAsync(Guid reviewId, CancellationToken cancellationToken = default) =>
        PublicReviewsOfActiveRestaurants().AnyAsync(review => review.Id == reviewId, cancellationToken);

    // Admin (Phase 9): no visibility filter, so hidden reviews and inactive restaurants are included.

    public async Task<IReadOnlyList<AdminReviewProjection>> ListForAdminAsync(
        Guid? restaurantId,
        Guid? userId,
        FeedCursor? after,
        int take,
        CancellationToken cancellationToken = default)
    {
        var reviews = _dbContext.Reviews.AsNoTracking();
        if (restaurantId is { } restaurant)
        {
            reviews = reviews.Where(review => review.RestaurantId == restaurant);
        }

        if (userId is { } user)
        {
            reviews = reviews.Where(review => review.UserId == user);
        }

        if (after is not null)
        {
            reviews = reviews.Where(review =>
                review.CreatedAt < after.CreatedAt
                || (review.CreatedAt == after.CreatedAt && review.Id < after.Id));
        }

        return await AdminItems(reviews
                .OrderByDescending(review => review.CreatedAt)
                .ThenByDescending(review => review.Id)
                .Take(take))
            .ToListAsync(cancellationToken);
    }

    public Task<AdminReviewProjection?> GetForAdminAsync(Guid reviewId, CancellationToken cancellationToken = default) =>
        AdminItems(_dbContext.Reviews.AsNoTracking().Where(review => review.Id == reviewId))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<bool?> SetHiddenAsync(
        Guid reviewId,
        bool hidden,
        Guid adminId,
        string? reason,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var review = await _dbContext.Reviews.SingleOrDefaultAsync(item => item.Id == reviewId, cancellationToken);
        if (review is null)
        {
            return null;
        }

        if ((review.HiddenAt is not null) == hidden)
        {
            return false;
        }

        // The visit token is never touched: a hidden review keeps its redeemed token, so it can't be rewritten.
        review.HiddenAt = hidden ? utcNow : null;
        review.HiddenByUserId = hidden ? adminId : null;
        review.HiddenReason = hidden ? reason : null;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private IQueryable<AdminReviewProjection> AdminItems(IQueryable<Review> reviews) =>
        reviews.Select(review => new AdminReviewProjection(
            review.Id,
            review.RestaurantId,
            review.Restaurant.Name,
            review.UserId,
            _dbContext.Users.Where(user => user.Id == review.UserId).Select(user => user.UserName).FirstOrDefault(),
            review.Rating,
            review.Text,
            review.CreatedAt,
            review.HiddenAt,
            review.HiddenReason,
            _dbContext.ReviewComments.Count(comment => comment.ReviewId == review.Id)));

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
