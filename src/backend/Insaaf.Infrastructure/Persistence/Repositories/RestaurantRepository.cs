using System.Linq.Expressions;
using Insaaf.Domain.Entities;
using Insaaf.Domain.Repositories;
using Insaaf.Domain.Repositories.Projections;
using Insaaf.Domain.Rules;
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
                // Aggregates are correlated subqueries in the same SELECT (no N+1), public reviews only.
                restaurant.Reviews.AsQueryable().Where(ReviewVisibility.IsPublic).Average(review => (decimal?)review.Rating),
                restaurant.Reviews.AsQueryable().Where(ReviewVisibility.IsPublic).Count(),
                restaurant.TimeZoneId,
                restaurant.OpeningHours
                    .Select(hours => new OpeningHoursProjection(hours.DayOfWeek, hours.OpensAt, hours.ClosesAt))
                    .ToList()))
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
                restaurant.Reviews.AsQueryable().Where(ReviewVisibility.IsPublic).Average(review => (decimal?)review.Rating),
                restaurant.Reviews.AsQueryable().Where(ReviewVisibility.IsPublic).Count(),
                restaurant.TimeZoneId,
                restaurant.OpeningHours
                    .Select(hours => new OpeningHoursProjection(hours.DayOfWeek, hours.OpensAt, hours.ClosesAt))
                    .ToList(),
                restaurant.Reviews.AsQueryable().Where(ReviewVisibility.IsPublic).Any()
                    ? new RatingBreakdownProjection(
                        restaurant.Reviews.AsQueryable().Where(ReviewVisibility.IsPublic).Average(review => (decimal)review.FoodRating),
                        restaurant.Reviews.AsQueryable().Where(ReviewVisibility.IsPublic).Average(review => (decimal)review.ServiceRating),
                        restaurant.Reviews.AsQueryable().Where(ReviewVisibility.IsPublic).Average(review => (decimal)review.ValueRating),
                        restaurant.Reviews.AsQueryable().Where(ReviewVisibility.IsPublic).Average(review => (decimal)review.AtmosphereRating))
                    : null))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<bool> IsActiveAsync(Guid id, CancellationToken cancellationToken = default) =>
        ActiveRestaurants().AnyAsync(restaurant => restaurant.Id == id, cancellationToken);

    public async Task<IReadOnlyList<string>> GetTimeZoneIdsAsync(CancellationToken cancellationToken = default)
    {
        return await ActiveRestaurants()
            .Where(restaurant => restaurant.TimeZoneId != null)
            .Select(restaurant => restaurant.TimeZoneId!)
            .Distinct()
            .ToListAsync(cancellationToken);
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

        // Compares the unrounded average of public reviews; restaurants without public reviews
        // have a null average and never match.
        if (criteria.MinRating is not null)
        {
            var minRating = criteria.MinRating.Value;
            query = query.Where(restaurant =>
                restaurant.Reviews.AsQueryable().Where(ReviewVisibility.IsPublic).Average(review => (decimal?)review.Rating)
                >= minRating);
        }

        if (criteria.OpenAt is not null)
        {
            query = query.Where(OpenInAnyZone(criteria.OpenAt));
        }

        return query;
    }

    // LINQ form of OpeningHoursRules.IsOpenAt, one term per time zone, combined with OR.
    // Restaurants without a time zone never match (their status is unknown).
    private static Expression<Func<Restaurant, bool>> OpenInAnyZone(IReadOnlyList<LocalTimeInZone> zones)
    {
        Expression<Func<Restaurant, bool>>? combined = null;

        foreach (var zone in zones)
        {
            var timeZoneId = zone.TimeZoneId;
            var day = zone.Day;
            var previousDay = OpeningHoursRules.PreviousDay(zone.Day);
            var time = zone.Time;

            Expression<Func<Restaurant, bool>> openInZone = restaurant =>
                restaurant.TimeZoneId == timeZoneId
                && restaurant.OpeningHours.Any(hours =>
                    (hours.DayOfWeek == day
                        && hours.OpensAt <= time
                        && (hours.ClosesAt <= hours.OpensAt || time < hours.ClosesAt))
                    || (hours.DayOfWeek == previousDay
                        && hours.ClosesAt <= hours.OpensAt
                        && time < hours.ClosesAt));

            combined = combined is null ? openInZone : OrElse(combined, openInZone);
        }

        return combined ?? (_ => false);
    }

    private static Expression<Func<Restaurant, bool>> OrElse(
        Expression<Func<Restaurant, bool>> left,
        Expression<Func<Restaurant, bool>> right)
    {
        var parameter = left.Parameters[0];
        var rightBody = new ParameterReplacer(right.Parameters[0], parameter).Visit(right.Body);
        return Expression.Lambda<Func<Restaurant, bool>>(Expression.OrElse(left.Body, rightBody), parameter);
    }

    private sealed class ParameterReplacer(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) =>
            node == from ? to : base.VisitParameter(node);
    }
}
