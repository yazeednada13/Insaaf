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

    public AuthService(
        IUserAccountService userAccountService,
        ITokenService tokenService,
        IRefreshTokenService refreshTokenService)
    {
        _userAccountService = userAccountService;
        _tokenService = tokenService;
        _refreshTokenService = refreshTokenService;
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
}
