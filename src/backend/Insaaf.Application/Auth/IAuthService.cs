using Insaaf.Application.Auth.Dtos;

namespace Insaaf.Application.Auth;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);

    Task<AuthResponseDto> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    Task<AuthResponseDto> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken = default);

    Task<UserDto> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
