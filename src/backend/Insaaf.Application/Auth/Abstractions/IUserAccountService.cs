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
}

public sealed record UserAccount(Guid Id, string Email);

public sealed record AccountCreationResult(bool Succeeded, Guid? UserId, IReadOnlyList<string> Errors);

public sealed record AccountSignInResult(bool Succeeded, UserAccount? User, IReadOnlyList<string> Errors);
