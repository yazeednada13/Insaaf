using Insaaf.Application.Photos;
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
        FeedCursorCodec.ValidateLimit(query.Limit);
        var cursor = FeedCursorCodec.Decode(query.Cursor);

        // One extra row shows whether another page exists.
        var rows = await _reviews.ListFollowingFeedAsync(viewerUserId, cursor, query.Limit + 1, cancellationToken);
        return ToResult(rows, query.Limit);
    }

    /// <summary>Builds a page from <paramref name="limit"/> + 1 rows.</summary>
    public static FeedResultDto ToResult(IReadOnlyList<FeedItemProjection> rows, int limit)
    {
        var page = rows.Take(limit).ToList();

        return new FeedResultDto(
            page.Select(ToDto).ToList(),
            rows.Count > limit ? FeedCursorCodec.Encode(page[^1].CreatedAt, page[^1].Id) : null);
    }

    private static FeedItemDto ToDto(FeedItemProjection item) =>
        new(
            item.Id,
            item.Rating,
            item.Text,
            // datetime2 comes back with Kind=Unspecified; all stored timestamps are UTC.
            DateTime.SpecifyKind(item.CreatedAt, DateTimeKind.Utc),
            item.IsVerified,
            new ReviewerDto(item.ReviewerId, item.ReviewerFullName, item.ReviewerUserName, item.ReviewerHasPublicProfile),
            new ReviewRestaurantDto(item.RestaurantId, item.RestaurantName, item.RestaurantCity, item.RestaurantAddress),
            item.LikeCount,
            item.CommentCount,
            item.IsLikedByViewer,
            ReviewPhotos.ToDtos(item.Photos));
}
