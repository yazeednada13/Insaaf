namespace Insaaf.Domain.Repositories;

/// <summary>The last feed item seen: the next page continues strictly after it.</summary>
public sealed record FeedCursor(DateTime CreatedAt, Guid Id);
