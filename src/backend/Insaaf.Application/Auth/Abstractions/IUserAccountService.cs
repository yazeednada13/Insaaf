namespace Insaaf.Application.Auth.Abstractions;

public interface IUserAccountService
{
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);

    Task<AccountCreationResult> CreateUserAsync(
        string email,
        string password,
        string fullName,
        string username,
        CancellationToken cancellationToken = default);

    Task<AccountSignInResult> ValidateCredentialsAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default);

    Task<UserAccount?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetRolesAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>A 6-digit reset code for the account with this email, or null when there is none (Phase 11).</summary>
    Task<PasswordResetCode?> GeneratePasswordResetCodeAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a new password when the code is valid (Phase 11). A wrong code counts as a failed attempt for the
    /// account lockout, so codes can't be guessed. Returns the account's id on success.
    /// </summary>
    Task<(PasswordChangeResult Result, Guid? UserId, IReadOnlyList<string> Errors)> ResetPasswordWithCodeAsync(
        string email,
        string code,
        string newPassword,
        CancellationToken cancellationToken = default);

    Task<(PasswordChangeResult Result, IReadOnlyList<string> Errors)> ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default);

    /// <summary>Updates the profile; the username goes through Identity's validation and normalization.</summary>
    Task<ProfileUpdateResult> UpdateProfileAsync(
        Guid userId,
        string fullName,
        string userName,
        string? bio,
        CancellationToken cancellationToken = default);
}

public enum PasswordChangeResult
{
    Changed,
    InvalidCode,
    WrongPassword,
    LockedOut,
    InvalidPassword,
    NotFound,
}

/// <summary>A password-reset code for an account (Phase 11). Never logged.</summary>
public sealed record PasswordResetCode(Guid UserId, string Email, string Code);

public enum ProfileUpdateResult
{
    Updated,
    UsernameTaken,
    InvalidUsername,
    NotFound,
}

public sealed record UserAccount(Guid Id, string Email);

public sealed record AccountCreationResult(bool Succeeded, Guid? UserId, IReadOnlyList<string> Errors);

/// <summary><see cref="IsSuspended"/> is true only when the password was correct but an Admin suspended the account.</summary>
public sealed record AccountSignInResult(bool Succeeded, UserAccount? User, IReadOnlyList<string> Errors, bool IsSuspended = false);
