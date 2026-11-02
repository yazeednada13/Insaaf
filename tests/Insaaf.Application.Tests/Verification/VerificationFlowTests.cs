using Insaaf.Application.Auth.Abstractions;
using Insaaf.Application.Verification;
using Insaaf.Application.Verification.Dtos;
using Insaaf.Domain.Entities;
using Insaaf.Domain.Enums;
using Insaaf.Domain.Repositories;
using Insaaf.Infrastructure.Identity;
using Insaaf.Infrastructure.Persistence;
using Insaaf.Infrastructure.Persistence.Repositories;
using Insaaf.Infrastructure.Verification;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Insaaf.Application.Tests.Verification;

/// <summary>Issuance, resolve, verified creation, revocation and concurrency on the real repositories (InMemory).</summary>
public sealed class VerificationFlowTests
{
    private static readonly DateTime Now = new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _databaseName = $"verification-{Guid.NewGuid():N}";
    private readonly VisitTokenCodec _codec = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Guid _adminId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _partnerId = Guid.NewGuid();
    private readonly Guid _otherPartnerId = Guid.NewGuid();
    private readonly Guid _nonPartnerId = Guid.NewGuid();

    public VerificationFlowTests()
    {
        _clock.Setup(clock => clock.UtcNow).Returns(Now);
        using var context = CreateContext();
        context.Users.AddRange(
            new ApplicationUser { Id = _adminId, UserName = "admin", Email = "admin@example.com" },
            new ApplicationUser { Id = _customerId, UserName = "customer", FullName = "Customer", Email = "c@example.com" });
        context.Restaurants.AddRange(
            CreateRestaurant(_partnerId, "partner-a", isPartner: true),
            CreateRestaurant(_otherPartnerId, "partner-b", isPartner: true),
            CreateRestaurant(_nonPartnerId, "not-partner", isPartner: false));
        context.SaveChanges();
    }

    // ── Issuance (channel-independent) ─────────────────────────────────────────

    [Theory]
    [InlineData(1)]
    [InlineData(25)]
    public async Task Issue_StoresOnlyHashes_WithChannelAndIssuerFromPrincipal(int count)
    {
        await using var context = CreateContext();

        var issued = await CreateIssuer(context).IssueAsync(
            IssuancePrincipal.ForAdmin(_adminId),
            new VisitTokenIssueRequest(_partnerId, count, Now.AddDays(90)));

        Assert.Equal(count, issued.Payloads.Count);
        Assert.All(issued.Payloads, payload => Assert.StartsWith(VisitPayload.Prefix, payload));

        var stored = await context.VisitTokens.AsNoTracking().ToListAsync();
        Assert.Equal(count, stored.Count);
        Assert.All(stored, token =>
        {
            Assert.Equal(_partnerId, token.RestaurantId);
            Assert.Equal(issued.IssuanceId, token.IssuanceId);
            Assert.Equal(VisitTokenIssuanceChannel.Manual, token.IssuanceChannel);
            Assert.Equal(_adminId, token.IssuedByUserId);
        });
        // The secret never reaches storage; only its hash does.
        var rawTokens = issued.Payloads.Select(payload => payload[VisitPayload.Prefix.Length..]).ToList();
        Assert.DoesNotContain(stored, token => rawTokens.Contains(token.TokenHash));
        Assert.Equal(
            rawTokens.Select(_codec.Hash).Order(),
            stored.Select(token => token.TokenHash).Order());
    }

    [Fact]
    public async Task Issue_RestrictedPrincipal_CannotIssueForAnotherRestaurant()
    {
        await using var context = CreateContext();
        // Built here only to prove the scope check; Phase 5 itself creates only Admin principals.
        var restrictedToA = new IssuancePrincipal(VisitTokenIssuanceChannel.Pos, null, _partnerId);

        var exception = await Assert.ThrowsAsync<VerificationException>(() =>
            CreateIssuer(context).IssueAsync(restrictedToA, new VisitTokenIssueRequest(_otherPartnerId, 1, Now.AddDays(1))));

        Assert.Equal("issuance_forbidden", exception.Code);
        Assert.Equal(403, exception.StatusCode);
        Assert.Empty(await context.VisitTokens.ToListAsync());

        var allowed = await CreateIssuer(context).IssueAsync(
            restrictedToA,
            new VisitTokenIssueRequest(_partnerId, 1, Now.AddDays(1)));
        Assert.Single(allowed.Payloads);
    }

    [Fact]
    public async Task Issue_RejectsNonPartnerInactiveUnknownAndInvalidRequests()
    {
        await using var context = CreateContext();
        var issuer = CreateIssuer(context);
        var admin = IssuancePrincipal.ForAdmin(_adminId);

        var notPartner = await Assert.ThrowsAsync<VerificationException>(() =>
            issuer.IssueAsync(admin, new VisitTokenIssueRequest(_nonPartnerId, 1, Now.AddDays(1))));
        Assert.Equal("restaurant_not_partner", notPartner.Code);

        var unknown = await Assert.ThrowsAsync<VerificationException>(() =>
            issuer.IssueAsync(admin, new VisitTokenIssueRequest(Guid.NewGuid(), 1, Now.AddDays(1))));
        Assert.Equal("not_found", unknown.Code);

        var tooMany = await Assert.ThrowsAsync<VerificationException>(() =>
            issuer.IssueAsync(admin, new VisitTokenIssueRequest(_partnerId, 501, Now.AddDays(1))));
        Assert.Equal("validation_error", tooMany.Code);

        var pastExpiry = await Assert.ThrowsAsync<VerificationException>(() =>
            issuer.IssueAsync(admin, new VisitTokenIssueRequest(_partnerId, 1, Now)));
        Assert.Equal("validation_error", pastExpiry.Code);

        (await context.Restaurants.SingleAsync(item => item.Id == _partnerId)).IsActive = false;
        await context.SaveChangesAsync();
        var inactive = await Assert.ThrowsAsync<VerificationException>(() =>
            issuer.IssueAsync(admin, new VisitTokenIssueRequest(_partnerId, 1, Now.AddDays(1))));
        Assert.Equal("restaurant_not_partner", inactive.Code);
    }

    // ── Resolve and verified creation ──────────────────────────────────────────

    [Fact]
    public async Task Resolve_ReturnsTokensRestaurant_WithoutRedeeming()
    {
        var payload = await IssueOneAsync(_partnerId);
        await using var context = CreateContext();

        var resolved = await CreateVerification(context).ResolveAsync(new ResolveVisitRequest(payload));

        Assert.Equal(_partnerId, resolved.Restaurant.Id);
        Assert.Equal("partner-a", resolved.Restaurant.Name);
        Assert.Null((await context.VisitTokens.AsNoTracking().SingleAsync()).RedeemedAt);
    }

    [Fact]
    public async Task Create_UsesTokensRestaurant_IsVerified_AndRedeemsOnce()
    {
        var payload = await IssueOneAsync(_partnerId);

        await using (var context = CreateContext())
        {
            var created = await CreateVerification(context).CreateReviewAsync(_customerId, ValidReview(payload));
            Assert.Equal(_partnerId, created.RestaurantId);
        }

        await using (var context = CreateContext())
        {
            var review = await context.Reviews.AsNoTracking().SingleAsync();
            var token = await context.VisitTokens.AsNoTracking().SingleAsync();
            Assert.Equal(_partnerId, review.RestaurantId);
            Assert.True(review.IsVerified);
            Assert.Equal(token.Id, review.VisitTokenId);
            Assert.Equal(_customerId, review.UserId);
            Assert.Equal(Now, token.RedeemedAt);
            Assert.Equal(_customerId, token.RedeemedByUserId);

            var second = await Assert.ThrowsAsync<VerificationException>(() =>
                CreateVerification(context).CreateReviewAsync(_customerId, ValidReview(payload)));
            Assert.Equal("token_used", second.Code);
            Assert.Equal(409, second.StatusCode);

            var resolveAgain = await Assert.ThrowsAsync<VerificationException>(() =>
                CreateVerification(context).ResolveAsync(new ResolveVisitRequest(payload)));
            Assert.Equal("token_used", resolveAgain.Code);
        }
    }

    [Fact]
    public async Task Create_ConcurrentRedemptions_OnlyOneReview()
    {
        var payload = await IssueOneAsync(_partnerId);

        // Two requests validate the same token before either saves.
        await using var first = CreateContext();
        await using var second = CreateContext();
        var hash = _codec.Hash(payload[VisitPayload.Prefix.Length..]);
        var firstToken = await new VisitTokenRepository(first).FindByHashAsync(hash);
        var secondToken = await new VisitTokenRepository(second).FindByHashAsync(hash);

        var firstResult = await new VisitTokenRepository(first).RedeemWithReviewAsync(firstToken!, NewReview(firstToken!));
        var secondResult = await new VisitTokenRepository(second).RedeemWithReviewAsync(secondToken!, NewReview(secondToken!));

        Assert.Equal(RedemptionResult.Redeemed, firstResult);
        Assert.Equal(RedemptionResult.AlreadyRedeemed, secondResult);
        await using var verify = CreateContext();
        Assert.Single(await verify.Reviews.ToListAsync());
    }

    [Fact]
    public async Task Tokens_AreRejected_WhenExpiredRevokedMalformedOrPartnershipEnded()
    {
        var expired = await IssueOneAsync(_partnerId);
        var revoked = await IssueOneAsync(_partnerId);
        var orphaned = await IssueOneAsync(_otherPartnerId);

        await using (var context = CreateContext())
        {
            var tokens = await context.VisitTokens.ToListAsync();
            tokens.Single(token => token.TokenHash == HashOf(expired)).ExpiresAt = Now;
            tokens.Single(token => token.TokenHash == HashOf(revoked)).RevokedAt = Now.AddMinutes(-1);
            (await context.Restaurants.SingleAsync(item => item.Id == _otherPartnerId)).IsPartner = false;
            await context.SaveChangesAsync();
        }

        await using var verifyContext = CreateContext();
        var service = CreateVerification(verifyContext);

        Assert.Equal("token_expired", (await Rejects(service, expired)).Code);
        Assert.Equal(410, (await Rejects(service, expired)).StatusCode);
        Assert.Equal("token_invalid", (await Rejects(service, revoked)).Code);
        Assert.Equal("token_invalid", (await Rejects(service, orphaned)).Code);
        Assert.Equal("token_invalid", (await Rejects(service, "https://example.com")).Code);
        Assert.Equal("token_invalid", (await Rejects(service, VisitPayload.Build(_codec.GenerateToken()))).Code);
        Assert.Empty(await verifyContext.Reviews.ToListAsync());
    }

    [Fact]
    public async Task Create_ValidatesContentBeforeTouchingTheToken()
    {
        var payload = await IssueOneAsync(_partnerId);
        await using var context = CreateContext();

        var exception = await Assert.ThrowsAsync<VerificationException>(() =>
            CreateVerification(context).CreateReviewAsync(
                _customerId,
                ValidReview(payload) with { FoodRating = 0, Text = new string('a', 501) }));

        Assert.Equal("validation_error", exception.Code);
        Assert.Null((await context.VisitTokens.AsNoTracking().SingleAsync()).RedeemedAt);
    }

    // ── Revocation and summary ─────────────────────────────────────────────────

    [Fact]
    public async Task Revoke_AffectsOnlyUnusedTokens_ByIssuanceOrRestaurant_AndSummaryCounts()
    {
        var redeemedPayload = await IssueOneAsync(_partnerId);
        await using (var context = CreateContext())
        {
            await CreateVerification(context).CreateReviewAsync(_customerId, ValidReview(redeemedPayload));
        }

        IssuedVisitTokensDto batch;
        await using (var context = CreateContext())
        {
            batch = await CreateIssuer(context).IssueAsync(
                IssuancePrincipal.ForAdmin(_adminId),
                new VisitTokenIssueRequest(_partnerId, 3, Now.AddDays(30)));
            await CreateIssuer(context).IssueAsync(
                IssuancePrincipal.ForAdmin(_adminId),
                new VisitTokenIssueRequest(_partnerId, 2, Now.AddDays(30)));
        }

        await using (var context = CreateContext())
        {
            var lifecycle = CreateLifecycle(context);
            var byIssuance = await lifecycle.RevokeUnusedAsync(IssuancePrincipal.ForAdmin(_adminId), _partnerId, batch.IssuanceId);
            Assert.Equal(3, byIssuance.RevokedCount);

            var summary = await lifecycle.GetSummaryAsync(IssuancePrincipal.ForAdmin(_adminId), _partnerId);
            Assert.Equal(new VisitTokenSummaryDto(Active: 2, Redeemed: 1, Expired: 0, Revoked: 3), summary);

            var rest = await lifecycle.RevokeUnusedAsync(IssuancePrincipal.ForAdmin(_adminId), _partnerId, null);
            Assert.Equal(2, rest.RevokedCount);
        }

        await using var verify = CreateContext();
        var redeemed = await verify.VisitTokens.AsNoTracking().SingleAsync(token => token.RedeemedAt != null);
        Assert.Null(redeemed.RevokedAt);
    }

    private async Task<string> IssueOneAsync(Guid restaurantId)
    {
        await using var context = CreateContext();
        var issued = await CreateIssuer(context).IssueAsync(
            IssuancePrincipal.ForAdmin(_adminId),
            new VisitTokenIssueRequest(restaurantId, 1, Now.AddDays(90)));
        return issued.Payloads.Single();
    }

    private string HashOf(string payload) => _codec.Hash(payload[VisitPayload.Prefix.Length..]);

    private async Task<VerificationException> Rejects(VerificationService service, string payload) =>
        await Assert.ThrowsAsync<VerificationException>(() =>
            service.CreateReviewAsync(_customerId, ValidReview(payload)));

    private static CreateVerifiedReviewRequest ValidReview(string token) =>
        new(token, Rating: 5, FoodRating: 5, ServiceRating: 4, ValueRating: 4, AtmosphereRating: 5, Text: "  تجربة رائعة  ");

    private Review NewReview(VisitToken token) =>
        new()
        {
            Id = Guid.NewGuid(),
            RestaurantId = token.RestaurantId,
            UserId = _customerId,
            Rating = 5,
            FoodRating = 5,
            ServiceRating = 5,
            ValueRating = 5,
            AtmosphereRating = 5,
            Text = "race",
            IsVerified = true,
            VisitTokenId = token.Id,
            CreatedAt = Now,
        };

    private AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_databaseName).Options);

    private VisitTokenIssuer CreateIssuer(AppDbContext context) =>
        new(new VisitTokenRepository(context), new RestaurantRepository(context), _codec, _clock.Object);

    private VerificationService CreateVerification(AppDbContext context) =>
        new(new VisitTokenRepository(context), new RestaurantRepository(context), _codec, _clock.Object);

    private VisitTokenLifecycleService CreateLifecycle(AppDbContext context) =>
        new(new VisitTokenRepository(context), new RestaurantRepository(context), _clock.Object);

    private static Restaurant CreateRestaurant(Guid id, string name, bool isPartner) =>
        new()
        {
            Id = id,
            Name = name,
            Description = "وصف",
            City = "عمّان",
            Latitude = 31.95,
            Longitude = 35.92,
            PriceLevel = PriceLevel.Moderate,
            IsActive = true,
            IsPartner = isPartner,
            CreatedAt = Now,
        };
}
