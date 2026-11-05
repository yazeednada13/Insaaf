using System.Globalization;
using System.Text;
using Insaaf.Application.Reviews.Dtos;
using Insaaf.Application.Social.Dtos;
using Insaaf.Domain.Repositories;
using Insaaf.Domain.Repositories.Projections;

namespace Insaaf.Application.Social;

public interface IFeedService
{
    /// <summary>Verified public reviews by users the viewer follows, newest first.</summary>
    Task<FeedResultDto> GetAsync(Guid viewerUserId, FeedQuery query, CancellationToken cancellationToken = default);
}

public sealed class FeedService : IFeedService
{
    public const int MaxLimit = 50;

    private readonly IReviewRepository _reviews;

    public FeedService(IReviewRepository reviews)
    {
        _reviews = reviews;
    }

    public async Task<FeedResultDto> GetAsync(
        Guid viewerUserId,
        FeedQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.Limit < 1 || query.Limit > MaxLimit)
        {
            throw new SocialException("validation_error", $"Limit must be between 1 and {MaxLimit}.");
        }

        var cursor = string.IsNullOrEmpty(query.Cursor) ? null : DecodeCursor(query.Cursor);

        // One extra row shows whether another page exists.
        var rows = await _reviews.ListFollowingFeedAsync(viewerUserId, cursor, query.Limit + 1, cancellationToken);
        var page = rows.Take(query.Limit).ToList();

        return new FeedResultDto(
            page.Select(ToDto).ToList(),
            rows.Count > query.Limit ? EncodeCursor(page[^1]) : null);
    }

    private static string EncodeCursor(FeedItemProjection item)
    {
        var value = $"{item.CreatedAt.Ticks.ToString(CultureInfo.InvariantCulture)}|{item.Id}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static FeedCursor DecodeCursor(string cursor)
    {
        try
        {
            var base64 = cursor.Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight(base64.Length + ((4 - (base64.Length % 4)) % 4), '=');
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(base64)).Split('|');

            if (parts.Length == 2
                && long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks)
                && ticks <= DateTime.MaxValue.Ticks
                && Guid.TryParse(parts[1], out var id))
            {
                return new FeedCursor(new DateTime(ticks, DateTimeKind.Utc), id);
            }
        }
        catch (FormatException)
        {
            // Falls through to the validation error below.
        }

        throw new SocialException("validation_error", "Cursor is not valid.");
    }

    private static FeedItemDto ToDto(FeedItemProjection item) =>
        new(
            item.Id,
            item.Rating,
            item.Text,
            // datetime2 comes back with Kind=Unspecified; all stored timestamps are UTC.
            DateTime.SpecifyKind(item.CreatedAt, DateTimeKind.Utc),
            item.IsVerified,
            new ReviewerDto(item.ReviewerId, item.ReviewerFullName, item.ReviewerUserName),
            new ReviewRestaurantDto(item.RestaurantId, item.RestaurantName, item.RestaurantCity, item.RestaurantAddress),
            item.LikeCount,
            item.CommentCount,
            item.IsLikedByViewer);
}
