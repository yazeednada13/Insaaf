using Insaaf.Application.Auth.Abstractions;
using Insaaf.Application.Social.Dtos;
using Insaaf.Domain.Enums;
using Insaaf.Domain.Repositories;

namespace Insaaf.Application.Social;

public interface ITasteSetupService
{
    Task<TastePreferencesDto> GetAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<TastePreferencesDto> SaveAsync(Guid userId, SetTastePreferencesRequest? request, CancellationToken cancellationToken = default);

    Task<SuggestedUsersDto> SuggestUsersAsync(Guid viewerUserId, SuggestedUsersQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// Taste Setup (Phase 11, Figma TasteSetupScreen): chosen categories feed «مقترح لك»; priorities are stored
/// only, because Figma and the spec define no rule that uses them. Suggested people come from real activity.
/// </summary>
public sealed class TasteSetupService : ITasteSetupService
{
    public const int MaxSuggestions = 20;
    public const int MaxCategories = 20;

    private static readonly Dictionary<string, TastePriority> PriorityCodes = new(StringComparer.Ordinal)
    {
        ["food_quality"] = TastePriority.FoodQuality,
        ["service"] = TastePriority.Service,
        ["price"] = TastePriority.Price,
        ["atmosphere"] = TastePriority.Atmosphere,
        ["location"] = TastePriority.Location,
    };

    private readonly IUserProfileRepository _profiles;
    private readonly IRestaurantRepository _restaurants;
    private readonly IDateTimeProvider _clock;

    public TasteSetupService(IUserProfileRepository profiles, IRestaurantRepository restaurants, IDateTimeProvider clock)
    {
        _profiles = profiles;
        _restaurants = restaurants;
        _clock = clock;
    }

    public async Task<TastePreferencesDto> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var preferences = await _profiles.GetTastePreferencesAsync(userId, cancellationToken)
            ?? throw new SocialException("not_found", "User was not found.", SocialStatusCodes.NotFound);

        return new TastePreferencesDto(
            preferences.Categories.Select(category => category.Slug).ToList(),
            PriorityCodes.Where(pair => preferences.Priorities.HasFlag(pair.Value)).Select(pair => pair.Key).ToList(),
            preferences.CompletedAt is not null);
    }

    public async Task<TastePreferencesDto> SaveAsync(
        Guid userId,
        SetTastePreferencesRequest? request,
        CancellationToken cancellationToken = default)
    {
        var slugs = request?.CategorySlugs?
            .Where(slug => !string.IsNullOrWhiteSpace(slug))
            .Select(slug => slug.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];

        // Figma: «التالي» stays disabled until at least one food is chosen.
        if (slugs.Count is 0 or > MaxCategories)
        {
            throw new SocialException("validation_error", $"Choose between 1 and {MaxCategories} categories.");
        }

        var categories = await _restaurants.GetCategoriesBySlugsAsync(slugs, cancellationToken);
        if (categories.Count != slugs.Count)
        {
            throw new SocialException("validation_error", "Unknown category.");
        }

        var priorities = TastePriority.None;
        foreach (var code in request!.Priorities ?? [])
        {
            if (!PriorityCodes.TryGetValue(code, out var priority))
            {
                throw new SocialException("validation_error", "Priorities must be food_quality, service, price, atmosphere or location.");
            }

            priorities |= priority;
        }

        if (!await _profiles.SaveTastePreferencesAsync(userId, categories.Select(category => category.Id).ToList(), priorities, _clock.UtcNow, cancellationToken))
        {
            throw new SocialException("not_found", "User was not found.", SocialStatusCodes.NotFound);
        }

        return await GetAsync(userId, cancellationToken);
    }

    public async Task<SuggestedUsersDto> SuggestUsersAsync(
        Guid viewerUserId,
        SuggestedUsersQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.Limit is < 1 or > MaxSuggestions)
        {
            throw new SocialException("validation_error", $"Limit must be between 1 and {MaxSuggestions}.");
        }

        var users = await _profiles.ListSuggestedAsync(viewerUserId, query.Limit, cancellationToken);
        return new SuggestedUsersDto(users
            .Select(user => new UserSearchItemDto(
                user.Id,
                user.FullName,
                user.UserName,
                user.ReviewCount,
                user.FollowerCount,
                false,
                UserAvatars.Url(user.Id, user.AvatarKey)))
            .ToList());
    }
}
