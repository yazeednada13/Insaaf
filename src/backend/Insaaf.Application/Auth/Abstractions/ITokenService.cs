namespace Insaaf.Application.Auth.Abstractions;

public interface ITokenService
{
    AccessTokenResult GenerateAccessToken(Guid userId, string email, IReadOnlyList<string> roles);
}

public sealed record AccessTokenResult(string Token, DateTime ExpiresAt);
