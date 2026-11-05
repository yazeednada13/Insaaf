using Insaaf.Domain.Entities;

namespace Insaaf.Domain.Repositories;

public interface IUserFollowRepository
{
    /// <summary>Adds the follow if it isn't there; a repeat or a concurrent duplicate is a no-op.</summary>
    Task AddIfMissingAsync(UserFollow follow, CancellationToken cancellationToken = default);

    /// <summary>Removes the follow if it exists.</summary>
    Task RemoveIfExistsAsync(Guid followerId, Guid followeeId, CancellationToken cancellationToken = default);
}
