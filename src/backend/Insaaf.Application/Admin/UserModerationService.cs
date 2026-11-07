using Insaaf.Application.Admin.Dtos;
using Insaaf.Application.Auth.Abstractions;
using Insaaf.Domain.Repositories;
using Insaaf.Domain.Repositories.Projections;

namespace Insaaf.Application.Admin;

public interface IUserModerationService
{
    Task<AdminUserSearchResultDto> SearchAsync(string? query, CancellationToken cancellationToken = default);

    Task<AdminUserDto> SetSuspensionAsync(
        Guid actorId,
        Guid userId,
        SetSuspensionRequest? request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Account lookup and suspension (Phase 9, tasks 9.4 and 9.5). A suspended account can't sign in or
/// refresh, and all its refresh tokens are revoked. An access token already issued stays valid until it
/// expires: there is no per-request check in the MVP (ADR-017). Content is left as it is.
/// </summary>
public sealed class UserModerationService : IUserModerationService
{
    public const int MinQueryLength = 2;
    public const int MaxQueryLength = 100;
    public const int MaxResults = 20;

    private readonly IUserModerationRepository _users;
    private readonly IRefreshTokenService _refreshTokens;
    private readonly IDateTimeProvider _clock;
    private readonly IAdminAuditLog _audit;

    public UserModerationService(
        IUserModerationRepository users,
        IRefreshTokenService refreshTokens,
        IDateTimeProvider clock,
        IAdminAuditLog audit)
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _clock = clock;
        _audit = audit;
    }

    public async Task<AdminUserSearchResultDto> SearchAsync(string? query, CancellationToken cancellationToken = default)
    {
        var term = query?.Trim();
        if (term is null || term.Length is < MinQueryLength or > MaxQueryLength)
        {
            throw new AdminException("validation_error", $"Query must be {MinQueryLength}–{MaxQueryLength} characters.");
        }

        var users = await _users.SearchAsync(term, MaxResults, cancellationToken);
        return new AdminUserSearchResultDto(users.Select(ToDto).ToList());
    }

    public async Task<AdminUserDto> SetSuspensionAsync(
        Guid actorId,
        Guid userId,
        SetSuspensionRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request?.Suspended is not { } suspended)
        {
            Record("user.suspension", actorId, userId, AdminAuditResult.ValidationFailed);
            throw new AdminException("validation_error", "suspended is required.");
        }

        var action = suspended ? "user.suspend" : "user.unsuspend";
        var target = await _users.GetAsync(userId, cancellationToken);
        if (target is null)
        {
            Record(action, actorId, userId, AdminAuditResult.NotFound);
            throw AdminException.NotFound("User");
        }

        if (suspended && target.IsAdmin)
        {
            Record(action, actorId, userId, AdminAuditResult.Refused);
            throw new AdminException("admin_target", "Admin accounts can't be suspended.", AdminStatusCodes.Forbidden);
        }

        var changed = await _users.SetSuspendedAsync(userId, suspended, actorId, _clock.UtcNow, cancellationToken);
        if (suspended)
        {
            // Revoked on every suspend request, so a retry also cleans up any token issued in between.
            await _refreshTokens.RevokeAllForUserAsync(userId, cancellationToken);
        }

        Record(action, actorId, userId, changed == true ? AdminAuditResult.Succeeded : AdminAuditResult.NoChange);
        return ToDto(await _users.GetAsync(userId, cancellationToken) ?? throw AdminException.NotFound("User"));
    }

    private void Record(string action, Guid actorId, Guid userId, string result) =>
        _audit.Record(new AdminAuditEntry(action, actorId, "user", userId, _clock.UtcNow, result));

    private static AdminUserDto ToDto(AdminUserProjection user) =>
        new(user.Id, user.UserName, user.FullName, user.Email, user.IsAdmin, user.SuspendedAt is not null, user.SuspendedAt);
}
