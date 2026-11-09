using System.Security.Cryptography;
using Insaaf.Domain.Entities;
using Insaaf.Domain.Enums;
using Insaaf.Domain.Repositories;
using Insaaf.Infrastructure.Persistence;
using Insaaf.Infrastructure.Persistence.Repositories;
using Insaaf.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Insaaf.IntegrationTests;

/// <summary>
/// The Phase 9A SQL Server probes as tests: real races on the photo repository with one DbContext per
/// caller (as in production). Rows belong to a run-tagged restaurant; no files are written.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class PhotoConcurrencyTests
{
    private readonly SqlServerApiFactory _factory;
    private readonly Api _api;

    public PhotoConcurrencyTests(SqlServerApiFactory factory)
    {
        _factory = factory;
        _api = new Api(factory);
    }

    [SqlServerFact]
    public async Task TenParallelReservations_OnOneToken_ReserveExactlyFive()
    {
        var (tokenId, userId) = await NewTokenAsync();

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 10).Select(async _ =>
        {
            await using var context = NewContext();
            return await new ReviewPhotoRepository(context).ReserveAsync(NewPhoto(tokenId, userId), DateTime.UtcNow);
        }));

        Assert.Equal(5, outcomes.Count(outcome => outcome == PhotoReservationResult.Reserved));
        Assert.Equal(5, outcomes.Count(outcome => outcome == PhotoReservationResult.LimitReached));
        var (slots, live, claim) = await StateAsync(tokenId);
        Assert.Equal(5, slots);
        Assert.Equal(5, live);
        Assert.Equal(userId, claim);
    }

    [SqlServerFact]
    public async Task FiveParallelRepeatsOfOneClientUploadId_MakeOneRowAndOneSlot()
    {
        var (tokenId, userId) = await NewTokenAsync();
        var clientUploadId = Guid.NewGuid();

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 5).Select(async _ =>
        {
            await using var context = NewContext();
            return await new ReviewPhotoRepository(context).ReserveAsync(NewPhoto(tokenId, userId, clientUploadId), DateTime.UtcNow);
        }));

        Assert.Equal(1, outcomes.Count(outcome => outcome == PhotoReservationResult.Reserved));
        Assert.Equal(4, outcomes.Count(outcome => outcome == PhotoReservationResult.Duplicate));
        var (slots, live, _) = await StateAsync(tokenId);
        Assert.Equal(1, slots);
        Assert.Equal(1, live);
    }

    [SqlServerFact]
    public async Task ThreeParallelLeaseTakes_OnAnExpiredLease_HaveOneWinner()
    {
        var (tokenId, userId) = await NewTokenAsync();
        var pending = NewPhoto(tokenId, userId);
        pending.LeaseExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await using (var context = NewContext())
        {
            Assert.Equal(PhotoReservationResult.Reserved, await new ReviewPhotoRepository(context).ReserveAsync(pending, DateTime.UtcNow));
        }

        var winners = await Task.WhenAll(Enumerable.Range(0, 3).Select(async _ =>
        {
            await using var context = NewContext();
            return await new ReviewPhotoRepository(context)
                .TryTakeLeaseAsync(pending.Id, Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow.AddMinutes(2));
        }));

        Assert.Equal(1, winners.Count(won => won));
    }

    [SqlServerFact]
    public async Task ParallelTombstoneAndPurge_ReleaseEverySlot_AndTheCounterRepairFindsNothing()
    {
        var (tokenId, userId) = await NewTokenAsync();
        var photos = Enumerable.Range(0, 5).Select(_ => NewPhoto(tokenId, userId)).ToList();
        foreach (var photo in photos)
        {
            await using var context = NewContext();
            Assert.Equal(PhotoReservationResult.Reserved, await new ReviewPhotoRepository(context).ReserveAsync(photo, DateTime.UtcNow));
        }

        await Task.WhenAll(photos.Select(async photo =>
        {
            await using var context = NewContext();
            var repository = new ReviewPhotoRepository(context);
            Assert.True(await repository.TombstoneAsync(photo.Id, tokenId, userId, DateTime.UtcNow));
            Assert.True(await repository.PurgeAsync(photo.Id, tokenId, DateTime.UtcNow));
        }));

        var (slots, live, claim) = await StateAsync(tokenId);
        Assert.Equal(0, slots);
        Assert.Equal(0, live);
        Assert.Null(claim);
        await using (var context = NewContext())
        {
            // Repair walks every token; this run's token is already consistent.
            await new ReviewPhotoRepository(context).RepairTokenCountersAsync();
        }

        Assert.Equal((0, 0, (Guid?)null), await StateAsync(tokenId));
    }

    private AppDbContext NewContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(_factory.ConnectionString).Options);

    private async Task<(Guid TokenId, Guid UserId)> NewTokenAsync()
    {
        var admin = await _api.AdminAsync();
        var restaurantId = await _api.CreatePartnerRestaurantAsync(admin);
        var user = await _api.RegisterAsync();

        await using var context = NewContext();
        var token = new VisitToken
        {
            Id = Guid.NewGuid(),
            RestaurantId = restaurantId,
            IssuanceId = Guid.NewGuid(),
            IssuanceChannel = VisitTokenIssuanceChannel.Manual,
            IssuedByUserId = admin.Id,
            TokenHash = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant(),
            IssuedAt = DateTime.UtcNow.AddMinutes(-1),
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            ConcurrencyStamp = Guid.NewGuid(),
        };
        context.VisitTokens.Add(token);
        await context.SaveChangesAsync();
        return (token.Id, user.Id);
    }

    private static ReviewPhoto NewPhoto(Guid tokenId, Guid userId, Guid? clientUploadId = null) => new()
    {
        Id = Guid.NewGuid(),
        ClientUploadId = clientUploadId ?? Guid.NewGuid(),
        VisitTokenId = tokenId,
        UploadedByUserId = userId,
        Status = ReviewPhotoStatus.Pending,
        ContentType = "image/jpeg",
        LeaseId = Guid.NewGuid(),
        LeaseExpiresAt = DateTime.UtcNow.AddMinutes(2),
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    private async Task<(int Slots, int Live, Guid? Claim)> StateAsync(Guid tokenId)
    {
        await using var context = NewContext();
        var token = await context.VisitTokens.AsNoTracking().SingleAsync(item => item.Id == tokenId);
        var live = await context.ReviewPhotos.CountAsync(photo => photo.VisitTokenId == tokenId && photo.ReviewId == null
            && photo.Status != ReviewPhotoStatus.Purged);
        return (token.PhotoSlotsUsed, live, token.PhotoClaimedByUserId);
    }
}
