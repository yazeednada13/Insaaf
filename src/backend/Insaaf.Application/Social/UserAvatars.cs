using Insaaf.Application.Reviews.Dtos;

namespace Insaaf.Application.Social;

/// <summary>
/// Profile photo URLs (Phase 11). Relative, like review photos. The stored key changes on every upload and
/// travels as <c>?v=</c>, so a new photo is a new URL for every image cache.
/// </summary>
public static class UserAvatars
{
    public static string? Url(Guid userId, string? avatarKey) =>
        avatarKey is null ? null : $"/api/v1/users/{userId}/avatar?v={Uri.EscapeDataString(avatarKey)}";

    /// <summary>A reviewer, comment author or actor; Admin accounts (no public profile) never show a photo.</summary>
    public static ReviewerDto Reviewer(Guid id, string? fullName, string? userName, bool hasPublicProfile, string? avatarKey) =>
        new(id, fullName, userName, hasPublicProfile, hasPublicProfile ? Url(id, avatarKey) : null);

    public static ReviewReplyDto? Reply(Insaaf.Domain.Repositories.Projections.ReviewReplyProjection? reply) =>
        reply is null ? null : new ReviewReplyDto(reply.Text, DateTime.SpecifyKind(reply.CreatedAt, DateTimeKind.Utc));
}
