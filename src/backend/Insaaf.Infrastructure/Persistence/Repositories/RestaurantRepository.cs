using Insaaf.Domain.Entities;
using Insaaf.Domain.Repositories;
using Insaaf.Domain.Repositories.Projections;
using Microsoft.EntityFrameworkCore;

namespace Insaaf.Infrastructure.Persistence.Repositories;

public sealed class RestaurantRepository : IRestaurantRepository
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 50;

    private readonly AppDbContext _dbContext;

    public RestaurantRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResult<RestaurantListItemProjection>> SearchAsync(
        RestaurantSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var page = criteria.Page < DefaultPage ? DefaultPage : criteria.Page;
        var pageSize = criteria.PageSize < 1
            ? DefaultPageSize
            : Math.Min(criteria.PageSize, MaxPageSize);

        var query = ApplyFilters(ActiveRestaurants(), criteria);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(restaurant => restaurant.Name)
            .ThenBy(restaurant => restaurant.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(restaurant => new RestaurantListItemProjection(
                restaurant.Id,
                restaurant.Name,
                restaurant.City,
                restaurant.Address,
                restaurant.Latitude,
                restaurant.Longitude,
                restaurant.PriceLevel,
                restaurant.ImageUrl,
                restaurant.RestaurantCategories
                    .OrderBy(join => join.Category.Name)
                    .Select(join => new CategoryProjection(
                        join.Category.Id,
                        join.Category.Name,
                        join.Category.Slug))
                    .ToList(),
                null,
                0))
            .ToListAsync(cancellationToken);

        return new PagedResult<RestaurantListItemProjection>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    public Task<RestaurantDetailProjection?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return ActiveRestaurants()
            .Where(restaurant => restaurant.Id == id)
            .Select(restaurant => new RestaurantDetailProjection(
                restaurant.Id,
                restaurant.Name,
                restaurant.Description,
                restaurant.City,
                restaurant.Address,
                restaurant.Latitude,
                restaurant.Longitude,
                restaurant.PriceLevel,
                restaurant.Phone,
                restaurant.ImageUrl,
                restaurant.RestaurantCategories
                    .OrderBy(join => join.Category.Name)
                    .Select(join => new CategoryProjection(
                        join.Category.Id,
                        join.Category.Name,
                        join.Category.Slug))
                    .ToList(),
                null,
                0))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private IQueryable<Restaurant> ActiveRestaurants() =>
        _dbContext.Restaurants
            .AsNoTracking()
            .Where(restaurant => restaurant.IsActive);

    private static IQueryable<Restaurant> ApplyFilters(
        IQueryable<Restaurant> query,
        RestaurantSearchCriteria criteria)
    {
        if (!string.IsNullOrWhiteSpace(criteria.Search))
        {
            var search = criteria.Search.Trim().ToLower();
            query = query.Where(restaurant =>
                restaurant.Name.ToLower().Contains(search)
                || restaurant.Description.ToLower().Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(criteria.City))
        {
            var city = criteria.City.Trim().ToLower();
            query = query.Where(restaurant => restaurant.City.ToLower() == city);
        }

        if (criteria.PriceLevel is not null)
        {
            var priceLevel = criteria.PriceLevel.Value;
            query = query.Where(restaurant => restaurant.PriceLevel == priceLevel);
        }

        if (!string.IsNullOrWhiteSpace(criteria.CategorySlug))
        {
            var slug = criteria.CategorySlug.Trim().ToLower();
            query = query.Where(restaurant =>
                restaurant.RestaurantCategories.Any(join => join.Category.Slug.ToLower() == slug));
        }

        // Phase 3: no Review entity. AverageRating is always null and ReviewCount is 0.
        // When minRating is set, restaurants with zero reviews are excluded (all rows today).
        // Phase 4 should replace this with a Reviews aggregate join.
        if (criteria.MinRating is not null)
        {
            query = query.Where(_ => false);
        }

        return query;
    }
}
