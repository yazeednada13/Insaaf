using Insaaf.Application.Social.Dtos;
using Insaaf.Domain.Repositories;
using Insaaf.Domain.Repositories.Projections;

namespace Insaaf.Application.Social;

public interface IUserTasteService
{
    Task<TasteDto> GetTasteAsync(Guid userId, Guid viewerUserId, CancellationToken cancellationToken = default);

    Task<TasteMatchDto> GetMatchAsync(Guid userId, Guid viewerUserId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Taste from verified public reviews of active restaurants (ADR-016). Shares become human labels,
/// not precise scores, and nothing is shown below <see cref="MinimumReviews"/> reviews.
/// </summary>
public sealed class UserTasteService : IUserTasteService
{
    public const int MinimumReviews = 3;
    public const int VeryHighShare = 40;
    public const int HighShare = 20;
    public const int MaxCategories = 5;

    private readonly IUserProfileRepository _profiles;

    public UserTasteService(IUserProfileRepository profiles)
    {
        _profiles = profiles;
    }

    public async Task<TasteDto> GetTasteAsync(
        Guid userId,
        Guid viewerUserId,
        CancellationToken cancellationToken = default)
    {
        await EnsureVisibleAsync(userId, viewerUserId, cancellationToken);
        var taste = await _profiles.GetTasteAsync(userId, cancellationToken);

        if (taste.ReviewCount < MinimumReviews)
        {
            return new TasteDto(taste.ReviewCount, false, []);
        }

        return new TasteDto(
            taste.ReviewCount,
            true,
            taste.Categories
                .OrderByDescending(category => category.ReviewCount)
                .ThenBy(category => category.Slug, StringComparer.Ordinal)
                .Take(MaxCategories)
                .Select(category =>
                {
                    var share = (int)Math.Round(100.0 * category.ReviewCount / taste.ReviewCount);
                    return new CategoryTasteDto(category.Name, category.Slug, share, Level(share));
                })
                .ToList());
    }

    public async Task<TasteMatchDto> GetMatchAsync(
        Guid userId,
        Guid viewerUserId,
        CancellationToken cancellationToken = default)
    {
        await EnsureVisibleAsync(userId, viewerUserId, cancellationToken);
        if (userId == viewerUserId)
        {
            return new TasteMatchDto(false, null, []);
        }

        var theirs = await _profiles.GetTasteAsync(userId, cancellationToken);
        var mine = await _profiles.GetTasteAsync(viewerUserId, cancellationToken);

        if (theirs.ReviewCount < MinimumReviews || mine.ReviewCount < MinimumReviews)
        {
            return new TasteMatchDto(false, null, []);
        }

        return Match(mine, theirs);
    }

    /// <summary>
    /// Per shared category: 100 − 25 × the difference of average ratings (a 4-point gap is 0%).
    /// The total weights each category by the smaller of the two review counts.
    /// </summary>
    public static TasteMatchDto Match(TasteProjection mine, TasteProjection theirs)
    {
        var shared = mine.Categories
            .Join(
                theirs.Categories,
                category => category.Slug,
                category => category.Slug,
                (a, b) => (a.Name, a.Slug, Weight: Math.Min(a.ReviewCount, b.ReviewCount),
                    Percentage: Math.Clamp(100 - (25 * Math.Abs(a.AverageRating - b.AverageRating)), 0, 100)))
            .ToList();

        if (shared.Count == 0)
        {
            return new TasteMatchDto(false, null, []);
        }

        var total = shared.Sum(category => category.Percentage * category.Weight) / shared.Sum(category => category.Weight);

        return new TasteMatchDto(
            true,
            (int)Math.Round(total),
            shared
                .OrderByDescending(category => category.Weight)
                .ThenBy(category => category.Slug, StringComparer.Ordinal)
                .Take(MaxCategories)
                .Select(category => new CategoryMatchDto(category.Name, category.Slug, (int)Math.Round(category.Percentage)))
                .ToList());
    }

    private static string Level(int share) =>
        share >= VeryHighShare ? "very_high" : share >= HighShare ? "high" : "medium";

    private async Task EnsureVisibleAsync(Guid userId, Guid viewerUserId, CancellationToken cancellationToken)
    {
        // Admin accounts aren't public; their own data stays reachable.
        if (userId != viewerUserId && !await _profiles.IsPublicAsync(userId, cancellationToken))
        {
            throw new SocialException("not_found", "User was not found.", SocialStatusCodes.NotFound);
        }
    }
}
