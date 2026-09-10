using Insaaf.Application.Auth.Abstractions;
using Insaaf.Domain.Constants;
using Insaaf.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace Insaaf.Infrastructure.Auth;

public sealed class IdentityUserAccountService : IUserAccountService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public IdentityUserAccountService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    public async Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        return user is not null;
    }

    public async Task<AccountCreationResult> CreateUserAsync(
        string email,
        string password,
        string fullName,
        string username,
        CancellationToken cancellationToken = default)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = username,
            Email = email,
            FullName = fullName,
            EmailConfirmed = true,
        };

        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            return new AccountCreationResult(
                false,
                null,
                result.Errors.Select(error => error.Description).ToArray());
        }

        var roleResult = await _userManager.AddToRoleAsync(user, Roles.User);
        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            return new AccountCreationResult(
                false,
                null,
                roleResult.Errors.Select(error => error.Description).ToArray());
        }

        return new AccountCreationResult(true, user.Id, Array.Empty<string>());
    }

    public async Task<AccountSignInResult> ValidateCredentialsAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            return new AccountSignInResult(false, null, Array.Empty<string>());
        }

        if (await _userManager.IsLockedOutAsync(user))
        {
            return new AccountSignInResult(false, null, Array.Empty<string>());
        }

        var signInResult = await _signInManager.CheckPasswordSignInAsync(
            user,
            password,
            lockoutOnFailure: true);

        if (signInResult.Succeeded)
        {
            return new AccountSignInResult(true, new UserAccount(user.Id, user.Email!), Array.Empty<string>());
        }

        if (signInResult.IsLockedOut)
        {
            return new AccountSignInResult(false, null, Array.Empty<string>());
        }

        return new AccountSignInResult(false, null, Array.Empty<string>());
    }

    public async Task<UserAccount?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        return user?.Email is null ? null : new UserAccount(user.Id, user.Email);
    }

    public async Task<IReadOnlyList<string>> GetRolesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return Array.Empty<string>();
        }

        var roles = await _userManager.GetRolesAsync(user);
        return roles.ToArray();
    }
}
