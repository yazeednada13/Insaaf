namespace Insaaf.Application.Auth.Abstractions;

public interface IRefreshTokenService
{
    Task<RefreshTokenIssueResult> IssueAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<RefreshTokenRotationResult> RotateAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);

    Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}

public sealed record RefreshTokenIssueResult(string Token, DateTime ExpiresAt);

public enum RefreshTokenFailureReason
{
    Invalid,
    Expired,
    Reused,
    /// <summary>The account is suspended by an Admin (Phase 9).</summary>
    Suspended
}

public sealed record RefreshTokenRotationResult(
    bool Succeeded,
    Guid? UserId,
    string? NewRefreshToken,
    DateTime? NewRefreshTokenExpiresAt,
    RefreshTokenFailureReason? FailureReason);
