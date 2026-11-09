using Insaaf.Domain.Entities;
using Insaaf.Domain.Enums;
using Insaaf.Domain.Repositories;
using Insaaf.Domain.Repositories.Projections;
using Insaaf.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace Insaaf.Infrastructure.Persistence.Repositories;

public sealed class UserProfileRepository : IUserProfileRepository
{
    private readonly AppDbContext _dbContext;

    public UserProfileRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<UserProfileProjection?> GetPublicAsync(
        Guid userId,
        Guid viewerUserId,
        CancellationToken cancellationToken = default) =>
        ToProfile(PublicUsers().Where(user => user.Id == userId), viewerUserId)
            .SingleOrDefaultAsync(cancellationToken);

    public Task<UserProfileProjection?> GetOwnAsync(Guid userId, CancellationToken cancellationToken = default) =>
        ToProfile(_dbContext.Users.AsNoTracking().Where(user => user.Id == userId), userId)
            .SingleOrDefaultAsync(cancellationToken);

    public Task<bool> IsPublicAsync(Guid userId, CancellationToken cancellationToken = default) =>
        PublicUsers().AnyAsync(user => user.Id == userId, cancellationToken);

    public Task<PagedResult<UserListItemProjection>> ListFollowersAsync(
        Guid userId,
        Guid viewerUserId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default) =>
        ListAsync(
            Follows(userId, followers: true)
                .Join(PublicUsers(), follow => follow.FollowerId, user => user.Id, (follow, user) => new FollowRow { CreatedAt = follow.CreatedAt, User = user }),
            viewerUserId,
            page,
            pageSize,
            cancellationToken);

    public Task<PagedResult<UserListItemProjection>> ListFollowingAsync(
        Guid userId,
        Guid viewerUserId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default) =>
        ListAsync(
            Follows(userId, followers: false)
                .Join(PublicUsers(), follow => follow.FolloweeId, user => user.Id, (follow, user) => new FollowRow { CreatedAt = follow.CreatedAt, User = user }),
            viewerUserId,
            page,
            pageSize,
            cancellationToken);

    public async Task<PagedResult<UserSearchItemProjection>> SearchAsync(
        string term,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var matches = PublicUsers().Where(user =>
            (user.FullName != null && user.FullName.Contains(term))
            || (user.UserName != null && user.UserName.Contains(term)));

        var totalCount = await matches.CountAsync(cancellationToken);

        var items = await matches
            .OrderBy(user => user.FullName)
            .ThenBy(user => user.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(user => new UserSearchItemProjection(
                user.Id,
                user.FullName,
                user.UserName,
                _dbContext.PublicReviewsOfActiveRestaurants().Count(review => review.UserId == user.Id),
                _dbContext.UserFollows.Count(follow =>
                    follow.FolloweeId == user.Id && !_dbContext.AdminUserIds().Contains(follow.FollowerId)),
                user.AvatarKey))
            .ToListAsync(cancellationToken);

        return new PagedResult<UserSearchItemProjection>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    public async Task<(bool Found, string? PreviousKey)> SetAvatarKeyAsync(
        Guid userId,
        string? avatarKey,
        CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null)
        {
            return (false, null);
        }

        var previous = user.AvatarKey;
        user.AvatarKey = avatarKey;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return (true, previous);
    }

    public async Task<TastePreferencesProjection?> GetTastePreferencesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.AsNoTracking()
            .Where(item => item.Id == userId)
            .Select(item => new { item.TastePriorities, item.TasteSetupCompletedAt })
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null)
        {
            return null;
        }

        var categories = await _dbContext.UserFoodPreferences.AsNoTracking()
            .Where(preference => preference.UserId == userId)
            .Join(_dbContext.Categories, preference => preference.CategoryId, category => category.Id, (_, category) => category)
            .OrderBy(category => category.Slug)
            .Select(category => new CategoryProjection(category.Id, category.Name, category.Slug))
            .ToListAsync(cancellationToken);

        return new TastePreferencesProjection(categories, user.TastePriorities, user.TasteSetupCompletedAt);
    }

    public async Task<bool> SaveTastePreferencesAsync(
        Guid userId,
        IReadOnlyCollection<Guid> categoryIds,
        TastePriority priorities,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null)
        {
            return false;
        }

        // Replace the whole set in one SaveChanges (one transaction).
        _dbContext.UserFoodPreferences.RemoveRange(
            await _dbContext.UserFoodPreferences.Where(preference => preference.UserId == userId).ToListAsync(cancellationToken));
        _dbContext.UserFoodPreferences.AddRange(categoryIds.Distinct()
            .Select(categoryId => new UserFoodPreference { UserId = userId, CategoryId = categoryId }));
        user.TastePriorities = priorities;
        user.TasteSetupCompletedAt ??= utcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<UserSearchItemProjection>> ListSuggestedAsync(
        Guid viewerUserId,
        int take,
        CancellationToken cancellationToken = default) =>
        await PublicUsers()
            .Where(user => user.Id != viewerUserId && user.SuspendedAt == null)
            .Where(user => !_dbContext.UserFollows.Any(follow => follow.FollowerId == viewerUserId && follow.FolloweeId == user.Id))
            .Select(user => new
            {
                User = user,
                Reviews = _dbContext.PublicReviewsOfActiveRestaurants().Count(review => review.UserId == user.Id),
            })
            .Where(row => row.Reviews > 0)
            .OrderByDescending(row => row.Reviews)
            .ThenBy(row => row.User.Id)
            .Take(take)
            .Select(row => new UserSearchItemProjection(
                row.User.Id,
                row.User.FullName,
                row.User.UserName,
                row.Reviews,
                _dbContext.UserFollows.Count(follow =>
                    follow.FolloweeId == row.User.Id && !_dbContext.AdminUserIds().Contains(follow.FollowerId)),
                row.User.AvatarKey))
            .ToListAsync(cancellationToken);

    public Task<NotificationSettingsProjection?> GetNotificationSettingsAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _dbContext.Users.AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new NotificationSettingsProjection(user.NotifyLikes, user.NotifyComments, user.NotifyFollows))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<bool> SetNotificationSettingsAsync(
        Guid userId,
        NotificationSettingsProjection settings,
        CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null)
        {
            return false;
        }

        user.NotifyLikes = settings.Likes;
        user.NotifyComments = settings.Comments;
        user.NotifyFollows = settings.Follows;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<string?> GetPublicAvatarKeyAsync(Guid userId, CancellationToken cancellationToken = default) =>
        PublicUsers().Where(user => user.Id == userId).Select(user => user.AvatarKey).SingleOrDefaultAsync(cancellationToken);

    public async Task<TasteProjection> GetTasteAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var reviews = _dbContext.PublicReviewsOfActiveRestaurants().Where(review => review.UserId == userId);

        var reviewCount = await reviews.CountAsync(cancellationToken);
        if (reviewCount == 0)
        {
            return new TasteProjection(0, []);
        }

        // A restaurant with two categories counts its review in both.
        var categories = await reviews
            .Join(
                _dbContext.RestaurantCategories,
                review => review.RestaurantId,
                join => join.RestaurantId,
                (review, join) => new { join.Category.Name, join.Category.Slug, review.Rating })
            .GroupBy(row => new { row.Name, row.Slug })
            .Select(group => new CategoryTasteProjection(
                group.Key.Name,
                group.Key.Slug,
                group.Count(),
                group.Average(row => (double)row.Rating)))
            .ToListAsync(cancellationToken);

        return new TasteProjection(reviewCount, categories);
    }

    private IQueryable<ApplicationUser> PublicUsers() =>
        _dbContext.Users.AsNoTracking().Where(user => !_dbContext.AdminUserIds().Contains(user.Id));

    private IQueryable<UserFollow> Follows(Guid userId, bool followers) =>
        followers
            ? _dbContext.UserFollows.AsNoTracking().Where(follow => follow.FolloweeId == userId)
            : _dbContext.UserFollows.AsNoTracking().Where(follow => follow.FollowerId == userId);

    // Counts leave Admin accounts out, so they always match the lists.
    private IQueryable<UserProfileProjection> ToProfile(IQueryable<ApplicationUser> users, Guid viewerUserId) =>
        users.Select(user => new UserProfileProjection(
            user.Id,
            user.FullName,
            user.UserName,
            user.Bio,
            _dbContext.PublicReviewsOfActiveRestaurants().Count(review => review.UserId == user.Id),
            _dbContext.UserFollows.Count(follow =>
                follow.FolloweeId == user.Id && !_dbContext.AdminUserIds().Contains(follow.FollowerId)),
            _dbContext.UserFollows.Count(follow =>
                follow.FollowerId == user.Id && !_dbContext.AdminUserIds().Contains(follow.FolloweeId)),
            _dbContext.UserFollows.Any(follow => follow.FollowerId == viewerUserId && follow.FolloweeId == user.Id),
            _dbContext.AdminUserIds().Contains(user.Id) ? null : user.AvatarKey));

    private async Task<PagedResult<UserListItemProjection>> ListAsync(
        IQueryable<FollowRow> rows,
        Guid viewerUserId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var totalCount = await rows.CountAsync(cancellationToken);

        // Deterministic order: CreatedAt can tie, the user id can't.
        var items = await rows
            .OrderByDescending(row => row.CreatedAt)
            .ThenByDescending(row => row.User.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(row => new UserListItemProjection(
                row.User.Id,
                row.User.FullName,
                row.User.UserName,
                _dbContext.UserFollows.Any(follow =>
                    follow.FollowerId == viewerUserId && follow.FolloweeId == row.User.Id),
                row.User.AvatarKey))
            .ToListAsync(cancellationToken);

        return new PagedResult<UserListItemProjection>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    // A class with settable members, because EF can translate member access on it (not on a constructor).
    private sealed class FollowRow
    {
        public required DateTime CreatedAt { get; init; }

        public required ApplicationUser User { get; init; }
    }
}
