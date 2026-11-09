using Insaaf.Domain.Entities;
using Insaaf.Domain.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Insaaf.Infrastructure.Persistence.Repositories;

public sealed class SavedRepository : ISavedRepository
{
    private readonly AppDbContext _dbContext;

    public SavedRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddRestaurantIfMissingAsync(SavedRestaurant saved, CancellationToken cancellationToken = default)
    {
        if (await _dbContext.SavedRestaurants.AnyAsync(
                item => item.UserId == saved.UserId && item.RestaurantId == saved.RestaurantId,
                cancellationToken))
        {
            return;
        }

        _dbContext.SavedRestaurants.Add(saved);
        await SaveIgnoringDuplicateAsync(cancellationToken);
    }

    public async Task RemoveRestaurantIfExistsAsync(Guid userId, Guid restaurantId, CancellationToken cancellationToken = default)
    {
        var saved = await _dbContext.SavedRestaurants.SingleOrDefaultAsync(
            item => item.UserId == userId && item.RestaurantId == restaurantId,
            cancellationToken);
        if (saved is not null)
        {
            _dbContext.SavedRestaurants.Remove(saved);
            await SaveIgnoringConcurrentDeleteAsync(cancellationToken);
        }
    }

    public Task<bool> IsRestaurantSavedAsync(Guid userId, Guid restaurantId, CancellationToken cancellationToken = default) =>
        _dbContext.SavedRestaurants.AnyAsync(item => item.UserId == userId && item.RestaurantId == restaurantId, cancellationToken);

    public async Task<PagedResult<Guid>> ListRestaurantIdsAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var saved = _dbContext.SavedRestaurants
            .AsNoTracking()
            .Where(item => item.UserId == userId
                && _dbContext.Restaurants.Any(restaurant => restaurant.Id == item.RestaurantId && restaurant.IsActive));

        var totalCount = await saved.CountAsync(cancellationToken);
        var ids = await saved
            .OrderByDescending(item => item.CreatedAt)
            .ThenBy(item => item.RestaurantId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => item.RestaurantId)
            .ToListAsync(cancellationToken);

        return new PagedResult<Guid> { Items = ids, Page = page, PageSize = pageSize, TotalCount = totalCount };
    }

    public async Task AddReviewIfMissingAsync(SavedReview saved, CancellationToken cancellationToken = default)
    {
        if (await _dbContext.SavedReviews.AnyAsync(
                item => item.UserId == saved.UserId && item.ReviewId == saved.ReviewId,
                cancellationToken))
        {
            return;
        }

        _dbContext.SavedReviews.Add(saved);
        await SaveIgnoringDuplicateAsync(cancellationToken);
    }

    public async Task RemoveReviewIfExistsAsync(Guid userId, Guid reviewId, CancellationToken cancellationToken = default)
    {
        var saved = await _dbContext.SavedReviews.SingleOrDefaultAsync(
            item => item.UserId == userId && item.ReviewId == reviewId,
            cancellationToken);
        if (saved is not null)
        {
            _dbContext.SavedReviews.Remove(saved);
            await SaveIgnoringConcurrentDeleteAsync(cancellationToken);
        }
    }

    private async Task SaveIgnoringDuplicateAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // A concurrent request saved it first: the primary key kept it to one row.
            _dbContext.ChangeTracker.Clear();
        }
    }

    private async Task SaveIgnoringConcurrentDeleteAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // A concurrent request removed it first.
            _dbContext.ChangeTracker.Clear();
        }
    }
}
