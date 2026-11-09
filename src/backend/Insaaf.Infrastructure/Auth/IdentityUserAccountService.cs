using Insaaf.Application.Auth.Abstractions;
using Insaaf.Domain.Constants;
using Insaaf.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

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

        // Suspension (Phase 9) is reported only after a correct password, so it doesn't reveal accounts.
        // It is separate from the lockout check above, which stays unchanged.
        if (signInResult.Succeeded && user.SuspendedAt is not null)
        {
            return new AccountSignInResult(false, null, Array.Empty<string>(), IsSuspended: true);
        }

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

    // Phase 11: passwords. The reset code comes from Identity's email token provider (a 6-digit time-based
    // code tied to the security stamp), so a successful reset invalidates it.
    private const string ResetPurpose = "ResetPassword";

    public async Task<PasswordResetCode?> GeneratePasswordResetCodeAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user?.Email is null)
        {
            return null;
        }

        var code = await _userManager.GenerateUserTokenAsync(user, TokenOptions.DefaultEmailProvider, ResetPurpose);
        return new PasswordResetCode(user.Id, user.Email, code);
    }

    public async Task<(PasswordChangeResult Result, Guid? UserId, IReadOnlyList<string> Errors)> ResetPasswordWithCodeAsync(
        string email,
        string code,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            return (PasswordChangeResult.InvalidCode, null, []);
        }

        if (await _userManager.IsLockedOutAsync(user))
        {
            return (PasswordChangeResult.LockedOut, null, []);
        }

        if (!await _userManager.VerifyUserTokenAsync(user, TokenOptions.DefaultEmailProvider, ResetPurpose, code))
        {
            await _userManager.AccessFailedAsync(user);
            return (PasswordChangeResult.InvalidCode, null, []);
        }

        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, resetToken, newPassword);
        if (!result.Succeeded)
        {
            return (PasswordChangeResult.InvalidPassword, null, result.Errors.Select(error => error.Description).ToArray());
        }

        // The owner proved control of the email: clear failed attempts.
        await _userManager.ResetAccessFailedCountAsync(user);
        return (PasswordChangeResult.Changed, user.Id, []);
    }

    public async Task<(PasswordChangeResult Result, IReadOnlyList<string> Errors)> ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return (PasswordChangeResult.NotFound, []);
        }

        if (!await _userManager.CheckPasswordAsync(user, currentPassword))
        {
            return (PasswordChangeResult.WrongPassword, []);
        }

        var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        return result.Succeeded
            ? (PasswordChangeResult.Changed, [])
            : (PasswordChangeResult.InvalidPassword, result.Errors.Select(error => error.Description).ToArray());
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

    public async Task<ProfileUpdateResult> UpdateProfileAsync(
        Guid userId,
        string fullName,
        string userName,
        string? bio,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return ProfileUpdateResult.NotFound;
        }

        user.FullName = fullName;
        user.Bio = bio;

        try
        {
            // UserManager validates, normalizes and checks uniqueness case-insensitively.
            var result = string.Equals(user.UserName, userName, StringComparison.Ordinal)
                ? await _userManager.UpdateAsync(user)
                : await _userManager.SetUserNameAsync(user, userName);

            if (result.Succeeded)
            {
                return ProfileUpdateResult.Updated;
            }

            var describer = _userManager.ErrorDescriber;
            return result.Errors.Any(error => error.Code == describer.DuplicateUserName(userName).Code)
                ? ProfileUpdateResult.UsernameTaken
                : ProfileUpdateResult.InvalidUsername;
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // Two users took the name at once: the unique normalized index decided.
            return ProfileUpdateResult.UsernameTaken;
        }
    }
}
