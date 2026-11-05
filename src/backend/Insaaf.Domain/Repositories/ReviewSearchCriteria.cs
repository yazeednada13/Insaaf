namespace Insaaf.Domain.Repositories;

public enum ReviewSort
{
    Newest,
    Highest,
    Lowest,
}

public sealed class ReviewSearchCriteria
{
    public required Guid RestaurantId { get; init; }

    /// <summary>The signed-in user, for the per-user like status.</summary>
    public Guid ViewerUserId { get; init; }

    public ReviewSort Sort { get; init; } = ReviewSort.Newest;

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}
