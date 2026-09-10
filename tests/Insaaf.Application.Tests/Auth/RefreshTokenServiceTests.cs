using Insaaf.Application.Auth.Abstractions;
using Insaaf.Infrastructure.Auth;
using Insaaf.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Insaaf.Application.Tests.Auth;

public sealed class RefreshTokenServiceTests
{
    [Fact]
    public async Task RotateAsync_ReturnsNewToken_WhenRefreshTokenIsValid()
    {
        await using var context = CreateContext();
        var dateTimeProvider = new FixedDateTimeProvider(new DateTime(2026, 8, 30, 12, 0, 0, DateTimeKind.Utc));
        var service = CreateService(context, dateTimeProvider);

        var userId = Guid.NewGuid();
        var issued = await service.IssueAsync(userId);
        var rotated = await service.RotateAsync(issued.Token);

        Assert.True(rotated.Succeeded);
        Assert.Equal(userId, rotated.UserId);
        Assert.NotEqual(issued.Token, rotated.NewRefreshToken);
    }

    [Fact]
    public async Task RotateAsync_DetectsReuse_WhenRevokedTokenIsPresentedAgain()
    {
        await using var context = CreateContext();
        var dateTimeProvider = new FixedDateTimeProvider(new DateTime(2026, 8, 30, 12, 0, 0, DateTimeKind.Utc));
        var service = CreateService(context, dateTimeProvider);

        var userId = Guid.NewGuid();
        var issued = await service.IssueAsync(userId);
        var firstRotation = await service.RotateAsync(issued.Token);
        var reuseAttempt = await service.RotateAsync(issued.Token);

        Assert.True(firstRotation.Succeeded);
        Assert.False(reuseAttempt.Succeeded);
        Assert.Equal(RefreshTokenFailureReason.Reused, reuseAttempt.FailureReason);

        var activeTokens = await context.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAt == null)
            .CountAsync();

        Assert.Equal(0, activeTokens);
    }

    [Fact]
    public async Task RotateAsync_ReturnsInvalid_WhenTokenWasRevokedIndependently()
    {
        await using var context = CreateContext();
        var dateTimeProvider = new FixedDateTimeProvider(new DateTime(2026, 8, 30, 12, 0, 0, DateTimeKind.Utc));
        var service = CreateService(context, dateTimeProvider);

        var userId = Guid.NewGuid();
        var issued = await service.IssueAsync(userId);

        var tokenHash = HashToken(issued.Token);
        var storedToken = await context.RefreshTokens.SingleAsync(token => token.TokenHash == tokenHash);
        storedToken.RevokedAt = dateTimeProvider.UtcNow;
        await context.SaveChangesAsync();

        var rotation = await service.RotateAsync(issued.Token);

        Assert.False(rotation.Succeeded);
        Assert.Equal(RefreshTokenFailureReason.Invalid, rotation.FailureReason);
    }

    [Fact]
    public async Task RotateAsync_ReturnsExpired_WhenTokenIsExpired()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 8, 30, 12, 0, 0, DateTimeKind.Utc);
        var dateTimeProvider = new FixedDateTimeProvider(now);
        var service = CreateService(context, dateTimeProvider);

        var userId = Guid.NewGuid();
        var issued = await service.IssueAsync(userId);

        dateTimeProvider.UtcNow = now.AddDays(8);
        var rotation = await service.RotateAsync(issued.Token);

        Assert.False(rotation.Succeeded);
        Assert.Equal(RefreshTokenFailureReason.Expired, rotation.FailureReason);
    }

    private static RefreshTokenService CreateService(AppDbContext context, FixedDateTimeProvider dateTimeProvider)
    {
        var options = Options.Create(new JwtOptions
        {
            RefreshTokenDays = 7,
        });

        return new RefreshTokenService(context, options, dateTimeProvider);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static string HashToken(string rawToken)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes);
    }

    private sealed class FixedDateTimeProvider : IDateTimeProvider
    {
        public FixedDateTimeProvider(DateTime utcNow) => UtcNow = utcNow;

        public DateTime UtcNow { get; set; }
    }
}
