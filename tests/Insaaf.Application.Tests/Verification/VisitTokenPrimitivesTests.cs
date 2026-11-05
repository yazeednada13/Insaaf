using Insaaf.Application.Verification;
using Insaaf.Domain.Entities;
using Insaaf.Domain.Rules;
using Insaaf.Infrastructure.Verification;

namespace Insaaf.Application.Tests.Verification;

public sealed class VisitTokenPrimitivesTests
{
    private static readonly DateTime Now = new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Rules_ValidBeforeExpiry()
    {
        Assert.Equal(VisitTokenState.Valid, VisitTokenRules.Evaluate(Token(expiresAt: Now.AddSeconds(1)), Now));
    }

    [Fact]
    public void Rules_ExpiredAtExactExpiry()
    {
        Assert.Equal(VisitTokenState.Expired, VisitTokenRules.Evaluate(Token(expiresAt: Now), Now));
    }

    [Fact]
    public void Rules_RedeemedIsUsed_EvenIfLaterExpiredOrRevoked()
    {
        var token = Token(expiresAt: Now.AddDays(-1));
        token.RedeemedAt = Now.AddDays(-2);
        token.RevokedAt = Now.AddDays(-1);

        Assert.Equal(VisitTokenState.Used, VisitTokenRules.Evaluate(token, Now));
    }

    [Fact]
    public void Rules_RevokedBeatsExpired()
    {
        var token = Token(expiresAt: Now.AddDays(-1));
        token.RevokedAt = Now.AddDays(-2);

        Assert.Equal(VisitTokenState.Revoked, VisitTokenRules.Evaluate(token, Now));
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void Rules_OnlyActivePartnersAreEligible(bool isActive, bool isPartner, bool expected)
    {
        Assert.Equal(expected, VisitTokenRules.IsEligibleRestaurant(isActive, isPartner));
    }

    [Fact]
    public void Codec_GeneratesUniqueUnguessableTokens()
    {
        var codec = new VisitTokenCodec();
        var tokens = Enumerable.Range(0, 10_000).Select(_ => codec.GenerateToken()).ToList();

        Assert.All(tokens, token => Assert.Equal(VisitPayload.TokenLength, token.Length));
        Assert.Equal(tokens.Count, tokens.Distinct(StringComparer.Ordinal).Count());
        Assert.All(tokens, token => Assert.True(VisitPayload.TryExtractToken(token, out _)));
    }

    [Fact]
    public void Codec_HashIsDeterministicSha256Hex_AndNeverTheToken()
    {
        var codec = new VisitTokenCodec();
        var token = codec.GenerateToken();

        var hash = codec.Hash(token);

        Assert.Equal(hash, codec.Hash(token));
        Assert.Equal(64, hash.Length);
        Assert.Matches("^[0-9A-F]{64}$", hash);
        Assert.NotEqual(token, hash);
    }

    [Fact]
    public void Payload_BuildsAndExtractsWithOrWithoutPrefix()
    {
        var token = new VisitTokenCodec().GenerateToken();
        var payload = VisitPayload.Build(token);

        Assert.Equal($"insaaf://visit/{token}", payload);
        Assert.True(VisitPayload.TryExtractToken(payload, out var fromPayload));
        Assert.Equal(token, fromPayload);
        Assert.True(VisitPayload.TryExtractToken($"  {token}  ", out var bare));
        Assert.Equal(token, bare);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://example.com/menu")]
    [InlineData("insaaf://visit/short")]
    [InlineData("insaaf://visit/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa+")]
    [InlineData("insaaf://restaurant/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void Payload_RejectsForeignOrMalformedInput(string? input)
    {
        Assert.False(VisitPayload.TryExtractToken(input, out _));
    }

    private static VisitToken Token(DateTime expiresAt) =>
        new()
        {
            Id = Guid.NewGuid(),
            IssuedAt = Now.AddDays(-30),
            ExpiresAt = expiresAt,
        };
}
