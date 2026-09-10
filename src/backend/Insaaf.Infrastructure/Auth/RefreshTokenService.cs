using System.Security.Cryptography;
using System.Text;
using Insaaf.Application.Auth.Abstractions;
using Insaaf.Domain.Entities;
using Insaaf.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Insaaf.Infrastructure.Auth;

public sealed class RefreshTokenService : IRefreshTokenService
{
    private readonly AppDbContext _dbContext;
    private readonly JwtOptions _jwtOptions;
    private readonly IDateTimeProvider _dateTimeProvider;

    public RefreshTokenService(
        AppDbContext dbContext,
        IOptions<JwtOptions> jwtOptions,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _jwtOptions = jwtOptions.Value;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<RefreshTokenIssueResult> IssueAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var rawToken = GenerateRawToken();
        var tokenHash = HashToken(rawToken);
        var expiresAt = _dateTimeProvider.UtcNow.AddDays(_jwtOptions.RefreshTokenDays);

        _dbContext.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            CreatedAt = _dateTimeProvider.UtcNow,
            ExpiresAt = expiresAt,
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        return new RefreshTokenIssueResult(rawToken, expiresAt);
    }

    public async Task<RefreshTokenRotationResult> RotateAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(refreshToken);
        var storedToken = await _dbContext.RefreshTokens
            .SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

        if (storedToken is null)
        {
            return new RefreshTokenRotationResult(false, null, null, null, RefreshTokenFailureReason.Invalid);
        }

        if (storedToken.RevokedAt is not null)
        {
            if (storedToken.ReplacedByTokenHash is not null)
            {
                await RevokeAllActiveTokensForUserAsync(storedToken.UserId, cancellationToken);
                return new RefreshTokenRotationResult(
                    false,
                    storedToken.UserId,
                    null,
                    null,
                    RefreshTokenFailureReason.Reused);
            }

            return new RefreshTokenRotationResult(
                false,
                storedToken.UserId,
                null,
                null,
                RefreshTokenFailureReason.Invalid);
        }

        if (storedToken.ExpiresAt <= _dateTimeProvider.UtcNow)
        {
            storedToken.RevokedAt = _dateTimeProvider.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return new RefreshTokenRotationResult(
                false,
                storedToken.UserId,
                null,
                null,
                RefreshTokenFailureReason.Expired);
        }

        var newRawToken = GenerateRawToken();
        var newTokenHash = HashToken(newRawToken);
        var newExpiresAt = _dateTimeProvider.UtcNow.AddDays(_jwtOptions.RefreshTokenDays);

        storedToken.RevokedAt = _dateTimeProvider.UtcNow;
        storedToken.ReplacedByTokenHash = newTokenHash;

        _dbContext.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = storedToken.UserId,
            TokenHash = newTokenHash,
            CreatedAt = _dateTimeProvider.UtcNow,
            ExpiresAt = newExpiresAt,
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new RefreshTokenRotationResult(
            true,
            storedToken.UserId,
            newRawToken,
            newExpiresAt,
            null);
    }

    public async Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await RevokeAllActiveTokensForUserAsync(userId, cancellationToken);
    }

    private async Task RevokeAllActiveTokensForUserAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var activeTokens = await _dbContext.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.RevokedAt = _dateTimeProvider.UtcNow;
        }

        if (activeTokens.Count > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static string GenerateRawToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }

    internal static string HashToken(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes);
    }
}
