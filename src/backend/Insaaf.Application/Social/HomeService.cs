using Insaaf.Application.Auth.Abstractions;
using Insaaf.Application.Restaurants;
using Insaaf.Application.Social.Dtos;
using Insaaf.Domain.Repositories;

namespace Insaaf.Application.Social;

public interface IHomeService
{
    /// <summary>"مقترح لك": restaurants in the viewer's reviewed categories they haven't reviewed yet.</summary>
    Task<HomeRestaurantsDto> GetSuggestedAsync(
        Guid viewerUserId,
        HomeRestaurantsQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>"رائج بالقرب منك": the most reviewed restaurants of the last 30 days.</summary>
    Task<HomeRestaurantsDto> GetTrendingAsync(HomeRestaurantsQuery query, CancellationToken cancellationToken = default);
}

public sealed class HomeService : IHomeService
{
    public const int MaxLimit = 20;
    public static readonly TimeSpan TrendingWindow = TimeSpan.FromDays(30);

    private readonly IRestaurantRepository _restaurants;
    private readonly IDateTimeProvider _clock;

    public HomeService(IRestaurantRepository restaurants, IDateTimeProvider clock)
    {
        _restaurants = restaurants;
        _clock = clock;
    }

    public async Task<HomeRestaurantsDto> GetSuggestedAsync(
        Guid viewerUserId,
        HomeRestaurantsQuery query,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(query.Limit);
        var ids = await _restaurants.ListSuggestedIdsAsync(viewerUserId, query.Limit, cancellationToken);
        return await ToDtoAsync(ids, cancellationToken);
    }

    public async Task<HomeRestaurantsDto> GetTrendingAsync(
        HomeRestaurantsQuery query,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(query.Limit);
        var ids = await _restaurants.ListTrendingIdsAsync(_clock.UtcNow - TrendingWindow, query.Limit, cancellationToken);
        return await ToDtoAsync(ids, cancellationToken);
    }

    private async Task<HomeRestaurantsDto> ToDtoAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        var utcNow = _clock.UtcNow;
        var items = await _restaurants.ListByIdsAsync(ids, cancellationToken);
        var byId = items.ToDictionary(item => item.Id);

        // The ranking query decides the order; the item query only fills in the details.
        return new HomeRestaurantsDto(ids
            .Where(byId.ContainsKey)
            .Select(id => RestaurantService.ToListItemDto(byId[id], utcNow))
            .ToList());
    }

    private static void ValidateLimit(int limit)
    {
        if (limit < 1 || limit > MaxLimit)
        {
            throw new SocialException("validation_error", $"Limit must be between 1 and {MaxLimit}.");
        }
    }
}
