namespace Insaaf.Application.Auth.Dtos;

public sealed record RegisterRequest(
    string Email,
    string Password,
    string FullName,
    string Username);

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshRequest(string RefreshToken);

public sealed record UserDto(Guid Id, string Email, IReadOnlyList<string> Roles);

public sealed record AuthResponseDto(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    UserDto User);
