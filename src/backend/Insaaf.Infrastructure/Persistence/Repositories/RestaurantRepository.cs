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
            .Select(ListItem)
            .ToListAsync(cancellationToken);

        return new PagedResult<RestaurantListItemProjection>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    public async Task<IReadOnlyList<RestaurantListItemProjection>> ListByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default) =>
        ids.Count == 0
            ? []
            : await ActiveRestaurants().Where(restaurant => ids.Contains(restaurant.Id)).Select(ListItem).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Guid>> ListSuggestedIdsAsync(
        Guid userId,
        int take,
        CancellationToken cancellationToken = default)
    {
        var reviewedIds = _dbContext.PublicReviewsOfActiveRestaurants()
            .Where(review => review.UserId == userId)
            .Select(review => review.RestaurantId);
        var categoryIds = _dbContext.RestaurantCategories
            .Where(join => reviewedIds.Contains(join.RestaurantId))
            .Select(join => join.CategoryId);
        var notReviewed = ActiveRestaurants().Where(restaurant => !reviewedIds.Contains(restaurant.Id));

        var suggested = await BestRated(notReviewed
                .Where(restaurant => restaurant.RestaurantCategories.Any(join => categoryIds.Contains(join.CategoryId))))
            .Take(take)
            .Select(restaurant => restaurant.Id)
            .ToListAsync(cancellationToken);

        return suggested.Count > 0
            ? suggested
            : await BestRated(notReviewed).Take(take).Select(restaurant => restaurant.Id).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> ListTrendingIdsAsync(
        DateTime since,
        int take,
        CancellationToken cancellationToken = default) =>
        await ActiveRestaurants()
            .OrderByDescending(restaurant =>
                restaurant.Reviews.AsQueryable().Where(ReviewVisibility.IsPublic).Count(review => review.CreatedAt >= since))
            .ThenByDescending(restaurant =>
                restaurant.Reviews.AsQueryable().Where(ReviewVisibility.IsPublic).Average(review => (decimal?)review.Rating) ?? 0)
            .ThenBy(restaurant => restaurant.Id)
            .Take(take)
            .Select(restaurant => restaurant.Id)
            .ToListAsync(cancellationToken);

    // Unrated restaurants sort last; the id makes ties deterministic.
    private static IOrderedQueryable<Restaurant> BestRated(IQueryable<Restaurant> restaurants) =>
        restaurants
            .OrderByDescending(restaurant =>
                restaurant.Reviews.AsQueryable().Where(ReviewVisibility.IsPublic).Average(review => (decimal?)review.Rating) ?? 0)
            .ThenByDescending(restaurant => restaurant.Reviews.AsQueryable().Where(ReviewVisibility.IsPublic).Count())
            .ThenBy(restaurant => restaurant.Id);

    private static readonly Expression<Func<Restaurant, RestaurantListItemProjection>> ListItem =
        restaurant => new RestaurantListItemProjection(
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
                    .ToList());

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

    public Task<RestaurantPartnershipProjection?> GetPartnershipAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        _dbContext.Restaurants
            .AsNoTracking()
            .Where(restaurant => restaurant.Id == id)
            .Select(restaurant => new RestaurantPartnershipProjection(
                restaurant.Id,
                restaurant.IsActive,
                restaurant.IsPartner,
                restaurant.PartnerSince))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<RestaurantPartnershipProjection?> SetPartnershipAsync(
        Guid id,
        bool isPartner,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var restaurant = await _dbContext.Restaurants.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (restaurant is null)
        {
            return null;
        }

        if (isPartner && !restaurant.IsPartner)
        {
            restaurant.PartnerSince = utcNow;
        }
        else if (!isPartner)
        {
            restaurant.PartnerSince = null;
        }

        restaurant.IsPartner = isPartner;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new RestaurantPartnershipProjection(
            restaurant.Id,
            restaurant.IsActive,
            restaurant.IsPartner,
            restaurant.PartnerSince);
    }

    // Admin (Phase 9).

    private static readonly Expression<Func<Restaurant, AdminRestaurantProjection>> AdminItem =
        restaurant => new AdminRestaurantProjection(
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
            restaurant.TimeZoneId,
            restaurant.IsActive,
            restaurant.IsPartner,
            restaurant.CreatedAt,
            restaurant.RestaurantCategories
                .OrderBy(join => join.Category.Name)
                .Select(join => new CategoryProjection(join.Category.Id, join.Category.Name, join.Category.Slug))
                .ToList(),
            restaurant.OpeningHours
                .Select(hours => new OpeningHoursProjection(hours.DayOfWeek, hours.OpensAt, hours.ClosesAt))
                .ToList(),
            restaurant.Reviews.AsQueryable().Where(ReviewVisibility.IsPublic).Count());

    public async Task<PagedResult<AdminRestaurantProjection>> ListForAdminAsync(
        bool includeInactive,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Restaurants.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(restaurant => restaurant.IsActive);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(restaurant => restaurant.Name)
            .ThenBy(restaurant => restaurant.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(AdminItem)
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminRestaurantProjection>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    public Task<AdminRestaurantProjection?> GetForAdminAsync(Guid id, CancellationToken cancellationToken = default) =>
        _dbContext.Restaurants
            .AsNoTracking()
            .Where(restaurant => restaurant.Id == id)
            .Select(AdminItem)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<CategoryProjection>> GetCategoriesBySlugsAsync(
        IReadOnlyCollection<string> slugs,
        CancellationToken cancellationToken = default)
    {
        var lowered = slugs.Select(slug => slug.ToLower()).ToList();
        return await _dbContext.Categories
            .AsNoTracking()
            .Where(category => lowered.Contains(category.Slug.ToLower()))
            .Select(category => new CategoryProjection(category.Id, category.Name, category.Slug))
            .ToListAsync(cancellationToken);
    }

    public async Task<Guid> CreateAsync(
        RestaurantWriteModel model,
        DateTime createdAt,
        CancellationToken cancellationToken = default)
    {
        var restaurant = new Restaurant
        {
            Id = Guid.NewGuid(),
            CreatedAt = createdAt,
            IsActive = true,
            IsPartner = false,
        };
        Apply(restaurant, model);
        _dbContext.Restaurants.Add(restaurant);
        AddChildren(restaurant.Id, model);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return restaurant.Id;
    }

    public async Task<bool> UpdateAsync(Guid id, RestaurantWriteModel model, CancellationToken cancellationToken = default)
    {
        var restaurant = await _dbContext.Restaurants.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (restaurant is null)
        {
            return false;
        }

        Apply(restaurant, model);

        // Categories and hours are replaced as sets, in the same SaveChanges as the restaurant row.
        // Category links have a composite key, so kept links stay tracked instead of being removed and re-added.
        var links = await _dbContext.RestaurantCategories.Where(join => join.RestaurantId == id).ToListAsync(cancellationToken);
        _dbContext.RestaurantCategories.RemoveRange(links.Where(join => !model.CategoryIds.Contains(join.CategoryId)));
        _dbContext.RestaurantOpeningHours.RemoveRange(
            await _dbContext.RestaurantOpeningHours.Where(hours => hours.RestaurantId == id).ToListAsync(cancellationToken));
        AddChildren(id, model with
        {
            CategoryIds = model.CategoryIds.Where(categoryId => links.All(join => join.CategoryId != categoryId)).ToList(),
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool?> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        var restaurant = await _dbContext.Restaurants.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (restaurant is null)
        {
            return null;
        }

        if (restaurant.IsActive == isActive)
        {
            return false;
        }

        restaurant.IsActive = isActive;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static void Apply(Restaurant restaurant, RestaurantWriteModel model)
    {
        restaurant.Name = model.Name;
        restaurant.Description = model.Description;
        restaurant.City = model.City;
        restaurant.Address = model.Address;
        restaurant.Latitude = model.Latitude;
        restaurant.Longitude = model.Longitude;
        restaurant.PriceLevel = model.PriceLevel;
        restaurant.Phone = model.Phone;
        restaurant.ImageUrl = model.ImageUrl;
        restaurant.TimeZoneId = model.TimeZoneId;
    }

    private void AddChildren(Guid restaurantId, RestaurantWriteModel model)
    {
        _dbContext.RestaurantCategories.AddRange(model.CategoryIds.Select(categoryId => new RestaurantCategory
        {
            RestaurantId = restaurantId,
            CategoryId = categoryId,
        }));
        _dbContext.RestaurantOpeningHours.AddRange(model.OpeningHours.Select(hours => new RestaurantOpeningHours
        {
            Id = Guid.NewGuid(),
            RestaurantId = restaurantId,
            DayOfWeek = hours.DayOfWeek,
            OpensAt = hours.OpensAt,
            ClosesAt = hours.ClosesAt,
        }));
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
