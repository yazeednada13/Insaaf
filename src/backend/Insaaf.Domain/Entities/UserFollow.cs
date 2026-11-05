namespace Insaaf.Domain.Entities;

/// <summary>
/// One user following another. The composite key (FollowerId, FolloweeId) makes a duplicate follow
/// impossible. Both are Identity users; the relationships are configured in Infrastructure.
/// </summary>
public sealed class UserFollow
{
    public Guid FollowerId { get; set; }

    public Guid FolloweeId { get; set; }

    public DateTime CreatedAt { get; set; }
}
