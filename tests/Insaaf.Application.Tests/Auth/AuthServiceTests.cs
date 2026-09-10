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

    private AuthService CreateService() =>
        new(_userAccountService.Object, _tokenService.Object, _refreshTokenService.Object);

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
