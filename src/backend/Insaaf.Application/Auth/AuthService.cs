using Insaaf.Application.Auth.Abstractions;
using Insaaf.Application.Auth.Dtos;
using Insaaf.Application.Auth.Validation;
using Insaaf.Domain.Constants;

namespace Insaaf.Application.Auth;

public sealed class AuthService : IAuthService
{
    private readonly IUserAccountService _userAccountService;
    private readonly ITokenService _tokenService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IEmailSender _emailSender;

    public const int MinPasswordLength = 8;

    public AuthService(
        IUserAccountService userAccountService,
        ITokenService tokenService,
        IRefreshTokenService refreshTokenService,
        IEmailSender emailSender)
    {
        _userAccountService = userAccountService;
        _tokenService = tokenService;
        _refreshTokenService = refreshTokenService;
        _emailSender = emailSender;
    }

    public async Task<ForgotPasswordResultDto> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var email = request.Email?.Trim();
        if (string.IsNullOrEmpty(email) || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email))
        {
            throw new AuthException("validation_error", "A valid email is required.");
        }

        var reset = await _userAccountService.GeneratePasswordResetCodeAsync(email, cancellationToken);
        if (reset is not null)
        {
            await _emailSender.SendAsync(
                new EmailMessage(
                    reset.Email,
                    "رمز استعادة كلمة المرور — إنصاف",
                    $"رمز استعادة كلمة المرور الخاص بك هو: {reset.Code}\n\nالرمز صالح لدقائق قليلة. إذا لم تطلب استعادة كلمة المرور، تجاهل هذه الرسالة."),
                cancellationToken);
        }

        return new ForgotPasswordResultDto(true);
    }

    public async Task<ResetPasswordResultDto> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var email = request.Email?.Trim();
        var code = request.Code?.Trim();
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(code))
        {
            throw new AuthException("validation_error", "Email and code are required.");
        }

        ValidateNewPassword(request.NewPassword);
        var (result, userId, errors) = await _userAccountService.ResetPasswordWithCodeAsync(email, code, request.NewPassword!, cancellationToken);
        switch (result)
        {
            case PasswordChangeResult.Changed:
                await _refreshTokenService.RevokeAllForUserAsync(userId!.Value, cancellationToken);
                return new ResetPasswordResultDto(true);
            case PasswordChangeResult.InvalidPassword:
                throw new AuthException("validation_error", string.Join(' ', errors));
            case PasswordChangeResult.LockedOut:
                throw new AuthException("too_many_attempts", "Too many attempts. Please try again later.");
            default:
                throw new AuthException("invalid_code", "The code is invalid or has expired.");
        }
    }

    public async Task<AuthResponseDto> ChangePasswordAsync(
        Guid userId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(request.CurrentPassword))
        {
            throw new AuthException("validation_error", "The current password is required.");
        }

        ValidateNewPassword(request.NewPassword);
        var (result, errors) = await _userAccountService.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword!, cancellationToken);
        switch (result)
        {
            case PasswordChangeResult.Changed:
                break;
            case PasswordChangeResult.WrongPassword:
                throw new AuthException("wrong_password", "The current password is incorrect.");
            case PasswordChangeResult.InvalidPassword:
                throw new AuthException("validation_error", string.Join(' ', errors));
            default:
                throw new AuthException("unauthorized", "Authentication is required.", AuthStatusCodes.Unauthorized);
        }

        // Every other device is signed out; this one gets fresh tokens.
        await _refreshTokenService.RevokeAllForUserAsync(userId, cancellationToken);
        var account = await _userAccountService.GetByIdAsync(userId, cancellationToken)
            ?? throw new AuthException("unauthorized", "Authentication is required.", AuthStatusCodes.Unauthorized);
        var roles = await _userAccountService.GetRolesAsync(userId, cancellationToken);
        return await IssueTokensAsync(userId, account.Email, roles, cancellationToken);
    }

    private static void ValidateNewPassword(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinPasswordLength)
        {
            throw new AuthException("validation_error", $"The new password must be at least {MinPasswordLength} characters.");
        }
    }

    public async Task<AuthResponseDto> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationErrors = AuthRequestValidator.ValidateRegister(request);
        if (validationErrors.Count > 0)
        {
            throw new AuthException("validation_error", string.Join(' ', validationErrors));
        }

        var normalizedEmail = request.Email.Trim();

        if (await _userAccountService.EmailExistsAsync(normalizedEmail, cancellationToken))
        {
            throw new AuthException("duplicate_email", "An account with this email already exists.");
        }

        var creation = await _userAccountService.CreateUserAsync(
            normalizedEmail,
            request.Password,
            request.FullName.Trim(),
            request.Username.Trim(),
            cancellationToken);

        if (!creation.Succeeded || creation.UserId is null)
        {
            var message = creation.Errors.Count > 0
                ? string.Join(' ', creation.Errors)
                : "Registration failed.";
            throw new AuthException("validation_error", message);
        }

        var userId = creation.UserId.Value;
        var roles = await _userAccountService.GetRolesAsync(userId, cancellationToken);

        return await IssueTokensAsync(userId, normalizedEmail, roles, cancellationToken);
    }

    public async Task<AuthResponseDto> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationErrors = AuthRequestValidator.ValidateLogin(request);
        if (validationErrors.Count > 0)
        {
            throw new AuthException("validation_error", string.Join(' ', validationErrors));
        }

        var signIn = await _userAccountService.ValidateCredentialsAsync(
            request.Email.Trim(),
            request.Password,
            cancellationToken);

        if (signIn.IsSuspended)
        {
            throw AccountSuspended();
        }

        if (!signIn.Succeeded || signIn.User is null)
        {
            throw new AuthException(
                "invalid_credentials",
                "Invalid email or password.",
                AuthStatusCodes.Unauthorized);
        }

        var roles = await _userAccountService.GetRolesAsync(signIn.User.Id, cancellationToken);

        return await IssueTokensAsync(signIn.User.Id, signIn.User.Email, roles, cancellationToken);
    }

    public async Task<AuthResponseDto> RefreshAsync(
        RefreshRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationErrors = AuthRequestValidator.ValidateRefresh(request);
        if (validationErrors.Count > 0)
        {
            throw new AuthException("validation_error", string.Join(' ', validationErrors));
        }

        var rotation = await _refreshTokenService.RotateAsync(request.RefreshToken, cancellationToken);

        if (rotation.FailureReason == RefreshTokenFailureReason.Suspended)
        {
            throw AccountSuspended();
        }

        if (!rotation.Succeeded || rotation.UserId is null)
        {
            var code = rotation.FailureReason switch
            {
                RefreshTokenFailureReason.Reused => "refresh_token_reused",
                RefreshTokenFailureReason.Expired => "invalid_refresh_token",
                _ => "invalid_refresh_token"
            };

            var statusCode = AuthStatusCodes.Unauthorized;

            throw new AuthException(code, "Refresh token is invalid or expired.", statusCode);
        }

        var user = await _userAccountService.GetByIdAsync(rotation.UserId.Value, cancellationToken);
        if (user is null)
        {
            throw new AuthException(
                "invalid_refresh_token",
                "Refresh token is invalid or expired.",
                AuthStatusCodes.Unauthorized);
        }

        var roles = await _userAccountService.GetRolesAsync(user.Id, cancellationToken);
        var access = _tokenService.GenerateAccessToken(user.Id, user.Email, roles);

        return new AuthResponseDto(
            access.Token,
            access.ExpiresAt,
            rotation.NewRefreshToken!,
            rotation.NewRefreshTokenExpiresAt!.Value,
            new UserDto(user.Id, user.Email, roles));
    }

    public async Task<UserDto> GetCurrentUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userAccountService.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            throw new AuthException(
                "unauthorized",
                "User is not authorized.",
                AuthStatusCodes.Unauthorized);
        }

        var roles = await _userAccountService.GetRolesAsync(userId, cancellationToken);
        return new UserDto(user.Id, user.Email, roles);
    }

    public Task LogoutAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _refreshTokenService.RevokeAllForUserAsync(userId, cancellationToken);

    private async Task<AuthResponseDto> IssueTokensAsync(
        Guid userId,
        string email,
        IReadOnlyList<string> roles,
        CancellationToken cancellationToken)
    {
        var access = _tokenService.GenerateAccessToken(userId, email, roles);
        var refresh = await _refreshTokenService.IssueAsync(userId, cancellationToken);

        return new AuthResponseDto(
            access.Token,
            access.ExpiresAt,
            refresh.Token,
            refresh.ExpiresAt,
            new UserDto(userId, email, roles));
    }

    private static AuthException AccountSuspended() =>
        new("account_suspended", "This account is suspended.", AuthStatusCodes.Forbidden);
}
