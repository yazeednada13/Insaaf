using Insaaf.Application.Auth.Dtos;

namespace Insaaf.Application.Auth;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);

    Task<AuthResponseDto> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    Task<AuthResponseDto> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken = default);

    Task<UserDto> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Revokes all the user's refresh tokens, which signs out every device. Repeat-safe.</summary>
    Task LogoutAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Sends a 6-digit reset code when an account has this email; the answer is the same either way.</summary>
    Task<ForgotPasswordResultDto> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default);

    /// <summary>Sets a new password with a valid code, then signs out every device.</summary>
    Task<ResetPasswordResultDto> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default);

    /// <summary>Changes the password, signs out every other device and returns new tokens for this one.</summary>
    Task<AuthResponseDto> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default);
}
