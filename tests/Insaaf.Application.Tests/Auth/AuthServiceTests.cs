using Insaaf.Application.Auth;
using Insaaf.Application.Auth.Abstractions;
using Insaaf.Application.Auth.Dtos;
using Moq;

namespace Insaaf.Application.Tests.Auth;

public sealed class AuthServiceTests
{
    private readonly Mock<IUserAccountService> _userAccountService = new();
    private readonly Mock<ITokenService> _tokenService = new();
    private readonly Mock<IRefreshTokenService> _refreshTokenService = new();
    private readonly Mock<IEmailSender> _emailSender = new();

    private AuthService CreateService() =>
        new(_userAccountService.Object, _tokenService.Object, _refreshTokenService.Object, _emailSender.Object);

    // ── Phase 11: passwords ─────────────────────────────────────────────────

    [Fact]
    public async Task ForgotPassword_AnswersTheSame_ForUnknownEmails_AndEmailsOnlyKnownOnes()
    {
        _userAccountService
            .Setup(service => service.GeneratePasswordResetCodeAsync("known@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PasswordResetCode(Guid.NewGuid(), "known@example.com", "123456"));

        var known = await CreateService().ForgotPasswordAsync(new ForgotPasswordRequest(" known@example.com "));
        var unknown = await CreateService().ForgotPasswordAsync(new ForgotPasswordRequest("nobody@example.com"));

        Assert.Equal(known, unknown);
        _emailSender.Verify(
            sender => sender.SendAsync(It.Is<EmailMessage>(message => message.To == "known@example.com" && message.Body.Contains("123456")), It.IsAny<CancellationToken>()),
            Times.Once);
        await Assert.ThrowsAsync<AuthException>(() => CreateService().ForgotPasswordAsync(new ForgotPasswordRequest("not-an-email")));
    }

    [Fact]
    public async Task ResetPassword_RevokesEverySession_OnlyWhenTheCodeIsValid()
    {
        var userId = Guid.NewGuid();
        _userAccountService
            .Setup(service => service.ResetPasswordWithCodeAsync("user@example.com", "111111", "NewPassword1!", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PasswordChangeResult.Changed, (Guid?)userId, (IReadOnlyList<string>)[]));
        _userAccountService
            .Setup(service => service.ResetPasswordWithCodeAsync("user@example.com", "999999", "NewPassword1!", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PasswordChangeResult.InvalidCode, (Guid?)null, (IReadOnlyList<string>)[]));

        var wrong = await Assert.ThrowsAsync<AuthException>(() =>
            CreateService().ResetPasswordAsync(new ResetPasswordRequest("user@example.com", "999999", "NewPassword1!")));
        Assert.Equal("invalid_code", wrong.Code);
        _refreshTokenService.Verify(service => service.RevokeAllForUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);

        var result = await CreateService().ResetPasswordAsync(new ResetPasswordRequest("user@example.com", "111111", "NewPassword1!"));
        Assert.True(result.PasswordReset);
        _refreshTokenService.Verify(service => service.RevokeAllForUserAsync(userId, It.IsAny<CancellationToken>()), Times.Once);

        var tooShort = await Assert.ThrowsAsync<AuthException>(() =>
            CreateService().ResetPasswordAsync(new ResetPasswordRequest("user@example.com", "111111", "short")));
        Assert.Equal("validation_error", tooShort.Code);
    }

    [Fact]
    public async Task ChangePassword_RefusesAWrongCurrentPassword()
    {
        var userId = Guid.NewGuid();
        _userAccountService
            .Setup(service => service.ChangePasswordAsync(userId, "Wrong1!aa", "NewPassword1!", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PasswordChangeResult.WrongPassword, (IReadOnlyList<string>)[]));

        var error = await Assert.ThrowsAsync<AuthException>(() =>
            CreateService().ChangePasswordAsync(userId, new ChangePasswordRequest("Wrong1!aa", "NewPassword1!")));

        Assert.Equal("wrong_password", error.Code);
        _refreshTokenService.Verify(service => service.RevokeAllForUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_ThrowsDuplicateEmail_WhenEmailExists()
    {
        _userAccountService
            .Setup(service => service.EmailExistsAsync("user@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = CreateService();

        var exception = await Assert.ThrowsAsync<AuthException>(() =>
            service.RegisterAsync(new RegisterRequest("user@example.com", "Password1!", "Test User", "testuser")));

        Assert.Equal("duplicate_email", exception.Code);
    }

    [Fact]
    public async Task LoginAsync_ThrowsInvalidCredentials_WhenSignInFails()
    {
        _userAccountService
            .Setup(service => service.ValidateCredentialsAsync("user@example.com", "wrong", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountSignInResult(false, null, Array.Empty<string>()));

        var service = CreateService();

        var exception = await Assert.ThrowsAsync<AuthException>(() =>
            service.LoginAsync(new LoginRequest("user@example.com", "wrong")));

        Assert.Equal("invalid_credentials", exception.Code);
    }

    [Fact]
    public async Task LoginAsync_ReturnsTokens_WhenCredentialsAreValid()
    {
        var userId = Guid.NewGuid();
        _userAccountService
            .Setup(service => service.ValidateCredentialsAsync("user@example.com", "Password1!", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountSignInResult(true, new UserAccount(userId, "user@example.com"), Array.Empty<string>()));
        _userAccountService
            .Setup(service => service.GetRolesAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "User" });
        _tokenService
            .Setup(service => service.GenerateAccessToken(userId, "user@example.com", It.IsAny<IReadOnlyList<string>>()))
            .Returns(new AccessTokenResult("access-token", DateTime.UtcNow.AddMinutes(15)));
        _refreshTokenService
            .Setup(service => service.IssueAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefreshTokenIssueResult("refresh-token", DateTime.UtcNow.AddDays(7)));

        var service = CreateService();
        var result = await service.LoginAsync(new LoginRequest("user@example.com", "Password1!"));

        Assert.Equal("access-token", result.AccessToken);
        Assert.Equal("refresh-token", result.RefreshToken);
        Assert.Equal(userId, result.User.Id);
    }
}
