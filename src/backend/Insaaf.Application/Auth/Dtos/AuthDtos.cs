namespace Insaaf.Application.Auth.Dtos;

public sealed record RegisterRequest(
    string Email,
    string Password,
    string FullName,
    string Username);

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshRequest(string RefreshToken);

public sealed record LogoutResultDto(bool SignedOut);

// Phase 11: passwords.

public sealed record ForgotPasswordRequest(string? Email);

/// <summary>Always true for a well-formed email, so the response never reveals whether an account exists.</summary>
public sealed record ForgotPasswordResultDto(bool CodeSent);

public sealed record ResetPasswordRequest(string? Email, string? Code, string? NewPassword);

public sealed record ResetPasswordResultDto(bool PasswordReset);

public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);

public sealed record UserDto(Guid Id, string Email, IReadOnlyList<string> Roles);

public sealed record AuthResponseDto(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    UserDto User);
