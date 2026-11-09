using Insaaf.Application.Admin;
using Insaaf.Application.Admin.Dtos;
using Insaaf.Application.Auth.Abstractions;
using Insaaf.Application.Photos;
using Insaaf.Application.Photos.Dtos;
using Insaaf.Application.Restaurants;
using Insaaf.Application.Reviews;
using Insaaf.Application.Tests.Admin;
using Insaaf.Application.Tests.Social;
using Insaaf.Application.Verification;
using Insaaf.Application.Verification.Dtos;
using Insaaf.Domain.Entities;
using Insaaf.Domain.Enums;
using Insaaf.Infrastructure.Identity;
using Insaaf.Infrastructure.Persistence;
using Insaaf.Infrastructure.Persistence.Repositories;
using Insaaf.Infrastructure.Photos;
using Insaaf.Infrastructure.Verification;
using Microsoft.EntityFrameworkCore;
using Moq;
using SkiaSharp;

namespace Insaaf.Application.Tests.Photos;

/// <summary>Phase 9A, tasks 9A.2–9A.4: uploads, claim, slots, attachment, removal and cleanup (InMemory).</summary>
public sealed class ReviewPhotoTests
{
    private static readonly DateTime Start = new(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _databaseName = $"photos-{Guid.NewGuid():N}";
    private readonly VisitTokenCodec _codec = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly FakePhotoStorage _storage = new();
    private readonly RecordingAuditLog _audit = new();
    private readonly Guid _adminId = Guid.NewGuid();
    private readonly Guid _authorId = Guid.NewGuid();
    private readonly Guid _otherId = Guid.NewGuid();
    private readonly Guid _restaurantId = Guid.NewGuid();
    private DateTime _now = Start;

    public ReviewPhotoTests()
    {
        _clock.Setup(clock => clock.UtcNow).Returns(() => _now);
        using var context = CreateContext();
        context.Users.AddRange(
            new ApplicationUser { Id = _adminId, UserName = "admin", Email = "admin@example.com" },
            new ApplicationUser { Id = _authorId, UserName = "author", FullName = "كاتب", Email = "a@example.com" },
            new ApplicationUser { Id = _otherId, UserName = "other", FullName = "آخر", Email = "o@example.com" });
        context.Restaurants.Add(new Restaurant
        {
            Id = _restaurantId,
            Name = "مطعم",
            Description = "وصف",
            City = "Amman",
            Latitude = 31.95,
            Longitude = 35.91,
            PriceLevel = PriceLevel.Moderate,
            IsActive = true,
            IsPartner = true,
            CreatedAt = Start.AddDays(-10),
        });
        context.SaveChanges();
        _storage.WriteTime = Start;
    }

    // ── Upload, claim and idempotency ─────────────────────────────────────────

    [Fact]
    public async Task Upload_StoresAReadyPhoto_ClaimsTheToken_AndNeverRedeemsIt()
    {
        var token = await IssueAsync();

        var result = await UploadAsync(_authorId, token, Guid.NewGuid());

        Assert.True(result.Created);
        var (row, visit) = await LoadAsync(result.Photo.Id);
        Assert.Equal(ReviewPhotoStatus.Ready, row.Status);
        Assert.True(_storage.Files.ContainsKey(row.StorageKey!));
        Assert.Equal(1, visit.PhotoSlotsUsed);
        Assert.Equal(_authorId, visit.PhotoClaimedByUserId);
        Assert.Null(visit.RedeemedAt);
    }

    [Fact]
    public async Task Upload_RepeatedWithTheSameClientUploadId_ReturnsTheSamePhoto_WithoutASecondSlot()
    {
        var token = await IssueAsync();
        var uploadId = Guid.NewGuid();

        var first = await UploadAsync(_authorId, token, uploadId);
        var second = await UploadAsync(_authorId, token, uploadId);

        Assert.False(second.Created);
        Assert.Equal(first.Photo.Id, second.Photo.Id);
        await using var context = CreateContext();
        Assert.Single(await context.ReviewPhotos.ToListAsync());
        Assert.Equal(1, (await context.VisitTokens.SingleAsync()).PhotoSlotsUsed);
    }

    [Fact]
    public async Task ClaimedToken_RefusesAnotherUsersUploadAndReview()
    {
        var token = await IssueAsync();
        await UploadAsync(_authorId, token, Guid.NewGuid());

        var upload = await Assert.ThrowsAsync<VerificationException>(() => UploadAsync(_otherId, token, Guid.NewGuid()));
        await using var context = CreateContext();
        var review = await Assert.ThrowsAsync<VerificationException>(() =>
            CreateVerification(context).CreateReviewAsync(_otherId, Review(token)));

        Assert.Equal(("token_claimed", 403), (upload.Code, upload.StatusCode));
        Assert.Equal("token_claimed", review.Code);
        Assert.Null((await context.VisitTokens.AsNoTracking().SingleAsync()).RedeemedAt);
    }

    [Fact]
    public async Task SixthUpload_IsRefused_AndRemovingOneFreesItsSlot()
    {
        var token = await IssueAsync();
        var ids = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            ids.Add((await UploadAsync(_authorId, token, Guid.NewGuid())).Photo.Id);
        }

        var sixth = await Assert.ThrowsAsync<VerificationException>(() => UploadAsync(_authorId, token, Guid.NewGuid()));
        await using (var context = CreateContext())
        {
            var removed = await CreatePhotoService(context).DeleteOwnAsync(_authorId, ids[0]);
            Assert.Equal(FileDeletionState.Completed, removed.FileDeletion);
        }

        var again = await UploadAsync(_authorId, token, Guid.NewGuid());

        Assert.Equal(("photo_limit", 409), (sixth.Code, sixth.StatusCode));
        Assert.True(again.Created);
        await using var check = CreateContext();
        Assert.Equal(5, (await check.VisitTokens.SingleAsync()).PhotoSlotsUsed);
    }

    [Fact]
    public async Task BadContent_IsRefusedBeforeAnyReservation()
    {
        var token = await IssueAsync();
        await using var context = CreateContext();
        var service = CreatePhotoService(context);

        var fake = await Assert.ThrowsAsync<VerificationException>(() =>
            service.UploadAsync(_authorId, token, Guid.NewGuid(), "not an image at all"u8.ToArray()));
        var large = await Assert.ThrowsAsync<VerificationException>(() =>
            service.UploadAsync(_authorId, token, Guid.NewGuid(), new byte[ReviewPhotoService.MaxBytes + 1]));
        var noId = await Assert.ThrowsAsync<VerificationException>(() =>
            service.UploadAsync(_authorId, token, null, Jpeg()));

        Assert.Equal(("unsupported_media_type", 415), (fake.Code, fake.StatusCode));
        Assert.Equal(("file_too_large", 413), (large.Code, large.StatusCode));
        Assert.Equal("validation_error", noId.Code);
        Assert.Empty(await context.ReviewPhotos.ToListAsync());
        var visit = await context.VisitTokens.AsNoTracking().SingleAsync();
        Assert.Equal((0, (Guid?)null), (visit.PhotoSlotsUsed, visit.PhotoClaimedByUserId));
    }

    // ── Attachment at review creation ─────────────────────────────────────────

    [Fact]
    public async Task Review_AttachesPhotosInOrder_ReleasesSlots_AndShowsThemOnTheDetail()
    {
        var token = await IssueAsync();
        var first = (await UploadAsync(_authorId, token, Guid.NewGuid())).Photo.Id;
        var second = (await UploadAsync(_authorId, token, Guid.NewGuid())).Photo.Id;

        Guid reviewId;
        await using (var context = CreateContext())
        {
            reviewId = (await CreateVerification(context).CreateReviewAsync(_authorId, Review(token, second, first))).ReviewId;
        }

        await using var check = CreateContext();
        var visit = await check.VisitTokens.SingleAsync();
        Assert.NotNull(visit.RedeemedAt);
        Assert.Equal(0, visit.PhotoSlotsUsed);
        var detail = await new ReviewService(new ReviewRepository(check), new RestaurantRepository(check)).GetByIdAsync(reviewId, _otherId);
        Assert.Equal([second, first], detail.Photos.Select(photo => photo.Id));
        Assert.Equal($"/api/v1/review-photos/{second}", detail.Photos[0].Url);
        // Photos never change ratings.
        var restaurant = await new RestaurantService(new RestaurantRepository(check), _clock.Object).GetByIdAsync(_restaurantId);
        Assert.Equal(4m, restaurant.AverageRating);
    }

    [Fact]
    public async Task Review_WithAnotherTokensOrUsersPhotos_IsRefused_AndTheTokenIsKept()
    {
        var token = await IssueAsync();
        var otherToken = await IssueAsync();
        var mine = (await UploadAsync(_authorId, token, Guid.NewGuid())).Photo.Id;
        var otherTokens = (await UploadAsync(_authorId, otherToken, Guid.NewGuid())).Photo.Id;
        var someoneElses = (await UploadAsync(_otherId, await IssueAsync(), Guid.NewGuid())).Photo.Id;

        foreach (var photoIds in new[] { new[] { mine, otherTokens }, new[] { someoneElses }, new[] { Guid.NewGuid() } })
        {
            await using var context = CreateContext();
            var error = await Assert.ThrowsAsync<VerificationException>(() =>
                CreateVerification(context).CreateReviewAsync(_authorId, Review(token, photoIds)));
            Assert.Equal("invalid_photo", error.Code);
        }

        await using (var context = CreateContext())
        {
            var duplicates = await Assert.ThrowsAsync<VerificationException>(() =>
                CreateVerification(context).CreateReviewAsync(_authorId, Review(token, mine, mine)));
            var tooMany = await Assert.ThrowsAsync<VerificationException>(() =>
                CreateVerification(context).CreateReviewAsync(_authorId, Review(token, Enumerable.Range(0, 6).Select(_ => Guid.NewGuid()).ToArray())));
            Assert.Equal("validation_error", duplicates.Code);
            Assert.Equal("validation_error", tooMany.Code);
        }

        await using var check = CreateContext();
        Assert.Null((await check.VisitTokens.SingleAsync(item => item.TokenHash == Hash(token))).RedeemedAt);
        Assert.Empty(await check.Reviews.ToListAsync());
        Assert.All(await check.ReviewPhotos.ToListAsync(), photo => Assert.Null(photo.ReviewId));
    }

    // ── Failures, leases and the fence ────────────────────────────────────────

    [Fact]
    public async Task SaveFailure_RollsTheRowToPurged_AndReleasesSlotAndClaim()
    {
        var token = await IssueAsync();
        _storage.FailSaves = true;

        var error = await Assert.ThrowsAsync<VerificationException>(() => UploadAsync(_authorId, token, Guid.NewGuid()));

        Assert.Equal(("upload_failed", 500), (error.Code, error.StatusCode));
        await using var context = CreateContext();
        Assert.Equal(ReviewPhotoStatus.Purged, (await context.ReviewPhotos.SingleAsync()).Status);
        var visit = await context.VisitTokens.SingleAsync();
        Assert.Equal((0, (Guid?)null), (visit.PhotoSlotsUsed, visit.PhotoClaimedByUserId));
    }

    [Fact]
    public async Task SaveAndDeleteFailures_KeepTheRowDeleting_UntilCleanupConfirmsTheFolderIsGone()
    {
        var token = await IssueAsync();
        _storage.FailSaves = true;
        _storage.FailDeletes = true;
        await Assert.ThrowsAsync<VerificationException>(() => UploadAsync(_authorId, token, Guid.NewGuid()));

        await using (var context = CreateContext())
        {
            var row = await context.ReviewPhotos.SingleAsync();
            Assert.Equal(ReviewPhotoStatus.Deleting, row.Status);
            Assert.Null(row.LeaseId);
            Assert.Equal(1, (await context.VisitTokens.SingleAsync()).PhotoSlotsUsed);
        }

        _storage.FailSaves = false;
        _storage.FailDeletes = false;
        await using (var context = CreateContext())
        {
            var report = await CreateCleanup(context).RunOnceAsync();
            Assert.Equal(1, report.Purged);
        }

        await using var check = CreateContext();
        Assert.Equal(ReviewPhotoStatus.Purged, (await check.ReviewPhotos.SingleAsync()).Status);
        Assert.Equal(0, (await check.VisitTokens.SingleAsync()).PhotoSlotsUsed);
    }

    [Fact]
    public async Task PendingRow_WithALiveLease_Refuses409_AndAfterExpiryARetryCompletesIt()
    {
        var token = await IssueAsync();
        var uploadId = Guid.NewGuid();
        var photoId = await InsertPendingAsync(token, uploadId, leaseExpiresAt: _now.AddMinutes(2));

        var busy = await Assert.ThrowsAsync<VerificationException>(() => UploadAsync(_authorId, token, uploadId));
        _now = _now.AddMinutes(3);
        var completed = await UploadAsync(_authorId, token, uploadId);

        Assert.Equal(("upload_in_progress", 409), (busy.Code, busy.StatusCode));
        Assert.Equal(photoId, completed.Photo.Id);
        await using var context = CreateContext();
        Assert.Equal(ReviewPhotoStatus.Ready, (await context.ReviewPhotos.SingleAsync()).Status);
        Assert.Equal(1, (await context.VisitTokens.SingleAsync()).PhotoSlotsUsed);
    }

    [Fact]
    public async Task Fence_AnAttemptThatLostItsLease_CannotCompleteOrChangeTheServedFile()
    {
        var token = await IssueAsync();
        var photoId = await InsertPendingAsync(token, Guid.NewGuid(), leaseExpiresAt: _now.AddMinutes(-1));
        var staleLease = (await LoadAsync(photoId)).Row.LeaseId!.Value;
        await using var context = CreateContext();
        var photos = new ReviewPhotoRepository(context);

        var newLease = Guid.NewGuid();
        Assert.True(await photos.TryTakeLeaseAsync(photoId, newLease, _now, _now.AddMinutes(2)));
        Assert.False(await photos.TryTakeLeaseAsync(photoId, Guid.NewGuid(), _now, _now.AddMinutes(2)));
        Assert.False(await photos.CompleteAsync(photoId, staleLease, "stale.jpg", 1, 1, 1, _now));
        Assert.True(await photos.CompleteAsync(photoId, newLease, _storage.KeyFor(photoId, newLease), 1, 1, 1, _now));

        Assert.Equal(_storage.KeyFor(photoId, newLease), (await LoadAsync(photoId)).Row.StorageKey);
    }

    [Fact]
    public async Task AbandonedAttempt_ReleasesItsLease_SoNobodyCanTakeOverOrComplete()
    {
        var token = await IssueAsync();
        var photoId = await InsertPendingAsync(token, Guid.NewGuid(), leaseExpiresAt: _now.AddMinutes(2));
        var lease = (await LoadAsync(photoId)).Row.LeaseId!.Value;
        await using var context = CreateContext();
        var photos = new ReviewPhotoRepository(context);

        Assert.True(await photos.AbandonAttemptAsync(photoId, lease, _now));

        _now = _now.AddMinutes(5);
        Assert.False(await photos.TryTakeLeaseAsync(photoId, Guid.NewGuid(), _now, _now.AddMinutes(2)));
        Assert.False(await photos.CompleteAsync(photoId, lease, "x.jpg", 1, 1, 1, _now));
        var row = (await LoadAsync(photoId)).Row;
        Assert.Equal((ReviewPhotoStatus.Deleting, (Guid?)null), (row.Status, row.LeaseId));
    }

    // ── Removal, visibility and cleanup ───────────────────────────────────────

    [Fact]
    public async Task AdminRemoval_ReportsFileDeletionHonestly_AndAuditsIt()
    {
        var (reviewId, photoIds) = await CreateReviewWithPhotosAsync(2);

        _storage.FailDeletes = true;
        PhotoRemovedDto pending;
        await using (var context = CreateContext())
        {
            pending = await CreateModeration(context).DeletePhotoAsync(_adminId, photoIds[0]);
        }

        _storage.FailDeletes = false;
        PhotoRemovedDto completed;
        await using (var context = CreateContext())
        {
            completed = await CreateModeration(context).DeletePhotoAsync(_adminId, photoIds[1]);
            await Assert.ThrowsAsync<AdminException>(() => CreateModeration(context).DeletePhotoAsync(_adminId, photoIds[1]));
        }

        Assert.Equal((true, FileDeletionState.Pending), (pending.Removed, pending.FileDeletion));
        Assert.Equal(FileDeletionState.Completed, completed.FileDeletion);
        Assert.Equal(
            [AdminAuditResult.RemovedFilePending, AdminAuditResult.Succeeded, AdminAuditResult.NotFound],
            _audit.Entries.Select(entry => entry.Result));
        Assert.All(_audit.Entries, entry => Assert.Equal(("photo.remove", "photo"), (entry.Action, entry.TargetType)));

        await using var check = CreateContext();
        var service = CreatePhotoService(check);
        Assert.Null(await service.OpenServableAsync(photoIds[0]));
        Assert.Null(await service.OpenServableAsync(photoIds[1]));
        Assert.Empty((await new ReviewService(new ReviewRepository(check), new RestaurantRepository(check)).GetByIdAsync(reviewId, _otherId)).Photos);
        Assert.Equal(ReviewPhotoStatus.Deleting, (await LoadAsync(photoIds[0])).Row.Status);
    }

    [Fact]
    public async Task HiddenReview_StopsServingItsPhotos_AndUnhideRestoresThem()
    {
        var (reviewId, photoIds) = await CreateReviewWithPhotosAsync(1);
        await using var context = CreateContext();
        var moderation = CreateModeration(context);
        var service = CreatePhotoService(context);
        await using (var served = await service.OpenServableAsync(photoIds[0]))
        {
            Assert.NotNull(served);
        }

        await moderation.SetVisibilityAsync(_adminId, reviewId, new SetReviewVisibilityRequest(true, null));
        Assert.Null(await service.OpenServableAsync(photoIds[0]));

        await moderation.SetVisibilityAsync(_adminId, reviewId, new SetReviewVisibilityRequest(false, null));
        await using var again = await service.OpenServableAsync(photoIds[0]);
        Assert.NotNull(again);
    }

    [Fact]
    public async Task UploaderDelete_OfAnAttachedPhoto_IsNotFound()
    {
        var (_, photoIds) = await CreateReviewWithPhotosAsync(1);
        await using var context = CreateContext();

        var error = await Assert.ThrowsAsync<VerificationException>(() => CreatePhotoService(context).DeleteOwnAsync(_authorId, photoIds[0]));

        Assert.Equal("not_found", error.Code);
    }

    [Fact]
    public async Task Cleanup_RemovesOldUnattachedPhotos_SweepsLateFiles_AndPrunesOldTombstones()
    {
        var token = await IssueAsync();
        var photoId = (await UploadAsync(_authorId, token, Guid.NewGuid())).Photo.Id;
        var unrelated = (await UploadAsync(_authorId, token, Guid.NewGuid())).Photo.Id;

        _now = _now.AddHours(25);
        await using (var context = CreateContext())
        {
            var report = await CreateCleanup(context).RunOnceAsync();
            Assert.Equal(2, report.Tombstoned);
            Assert.Equal(2, report.Purged);
        }

        // A late writer adds a file into the purged photo's folder; the tombstone still tracks it.
        _storage.WriteTime = _now;
        await _storage.SaveAttemptAsync(photoId, Guid.NewGuid(), new byte[] { 1 });
        await using (var context = CreateContext())
        {
            var report = await CreateCleanup(context).RunOnceAsync();
            Assert.Equal(1, report.FoldersSwept);
        }

        Assert.False(_storage.HasFolder(photoId));
        _now = _now.AddDays(31);
        await using (var context = CreateContext())
        {
            Assert.Equal(2, (await CreateCleanup(context).RunOnceAsync()).Pruned);
        }

        await using var check = CreateContext();
        Assert.Empty(await check.ReviewPhotos.Where(photo => photo.Id == photoId || photo.Id == unrelated).ToListAsync());
        var visit = await check.VisitTokens.SingleAsync();
        Assert.Equal((0, (Guid?)null), (visit.PhotoSlotsUsed, visit.PhotoClaimedByUserId));
    }

    [Fact]
    public async Task Cleanup_SweepsSupersededAttemptFiles_ButKeepsTheServedOneAndItsThumbnail()
    {
        var token = await IssueAsync();
        var photoId = (await UploadAsync(_authorId, token, Guid.NewGuid())).Photo.Id;
        var served = (await LoadAsync(photoId)).Row.StorageKey!;
        var superseded = Guid.NewGuid();
        await _storage.SaveThumbnailAsync(photoId, superseded, new byte[] { 1 });
        await _storage.SaveAttemptAsync(photoId, superseded, new byte[] { 1 });

        _now = _now.AddMinutes(11);
        await using var context = CreateContext();
        var report = await CreateCleanup(context).RunOnceAsync();

        Assert.Equal(2, report.FilesSwept);
        Assert.Equal(
            new[] { served, ReviewPhotos.ThumbnailKey(served) }.Order(),
            _storage.Files.Keys.Order());
    }

    [Fact]
    public async Task Thumbnail_IsServedWhenAsked_AndFallsBackToTheFullImageWhenMissing()
    {
        var (_, photoIds) = await CreateReviewWithPhotosAsync(1);
        var photoId = photoIds[0];
        var key = (await LoadAsync(photoId)).Row.StorageKey!;
        await using var context = CreateContext();
        var service = CreatePhotoService(context);

        await using (var full = await service.OpenServableAsync(photoId))
        await using (var small = await service.OpenServableAsync(photoId, thumbnail: true))
        {
            Assert.NotEqual(full!.ETag, small!.ETag);
            Assert.Contains("thumb", small.ETag);
        }

        // A photo from before Phase 10 has no thumbnail file: the full image is served for both.
        _storage.Files.Remove(ReviewPhotos.ThumbnailKey(key));
        await using var fallback = await service.OpenServableAsync(photoId, thumbnail: true);
        Assert.NotNull(fallback);
        Assert.DoesNotContain("thumb", fallback.ETag);
    }

    [Fact]
    public void Model_HasNoCascadingForeignKeyOnReviewPhotos()
    {
        using var context = CreateContext();

        var foreignKeys = context.Model.FindEntityType(typeof(ReviewPhoto))!.GetForeignKeys().ToList();

        Assert.Equal(3, foreignKeys.Count);
        Assert.All(foreignKeys, foreignKey => Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior));
        Assert.DoesNotContain(
            context.Model.GetEntityTypes().SelectMany(type => type.GetForeignKeys()),
            foreignKey => foreignKey.DeclaringEntityType.ClrType == typeof(ReviewPhoto) && foreignKey.DeleteBehavior == DeleteBehavior.Cascade);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<(Guid ReviewId, IReadOnlyList<Guid> PhotoIds)> CreateReviewWithPhotosAsync(int count)
    {
        var token = await IssueAsync();
        var ids = new List<Guid>();
        for (var i = 0; i < count; i++)
        {
            ids.Add((await UploadAsync(_authorId, token, Guid.NewGuid())).Photo.Id);
        }

        await using var context = CreateContext();
        var created = await CreateVerification(context).CreateReviewAsync(_authorId, Review(token, ids.ToArray()));
        return (created.ReviewId, ids);
    }

    private async Task<Guid> InsertPendingAsync(string token, Guid uploadId, DateTime leaseExpiresAt)
    {
        await using var context = CreateContext();
        var visit = await context.VisitTokens.SingleAsync(item => item.TokenHash == Hash(token));
        visit.PhotoSlotsUsed++;
        visit.PhotoClaimedByUserId = _authorId;
        var photo = new ReviewPhoto
        {
            Id = Guid.NewGuid(),
            ClientUploadId = uploadId,
            VisitTokenId = visit.Id,
            UploadedByUserId = _authorId,
            Status = ReviewPhotoStatus.Pending,
            LeaseId = Guid.NewGuid(),
            LeaseExpiresAt = leaseExpiresAt,
            CreatedAt = _now,
            UpdatedAt = _now,
        };
        context.ReviewPhotos.Add(photo);
        await context.SaveChangesAsync();
        return photo.Id;
    }

    private async Task<PhotoUploadResult> UploadAsync(Guid userId, string token, Guid uploadId)
    {
        await using var context = CreateContext();
        return await CreatePhotoService(context).UploadAsync(userId, token, uploadId, Jpeg());
    }

    private async Task<(ReviewPhoto Row, VisitToken Visit)> LoadAsync(Guid photoId)
    {
        await using var context = CreateContext();
        var row = await context.ReviewPhotos.AsNoTracking().SingleAsync(photo => photo.Id == photoId);
        var visit = await context.VisitTokens.AsNoTracking().SingleAsync(token => token.Id == row.VisitTokenId);
        return (row, visit);
    }

    private async Task<string> IssueAsync()
    {
        await using var context = CreateContext();
        var issued = await new VisitTokenIssuer(new VisitTokenRepository(context), new RestaurantRepository(context), _codec, _clock.Object)
            .IssueAsync(IssuancePrincipal.ForAdmin(_adminId), new VisitTokenIssueRequest(_restaurantId, 1, Start.AddDays(90)));
        return issued.Payloads.Single();
    }

    private string Hash(string payload) => _codec.Hash(payload[VisitPayload.Prefix.Length..]);

    private static CreateVerifiedReviewRequest Review(string token, params Guid[] photoIds) =>
        new(token, 4, 4, 4, 4, 4, "تقييم مع صور", photoIds);

    private static byte[] Jpeg()
    {
        using var bitmap = new SKBitmap(40, 30);
        bitmap.Erase(SKColors.Orange);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
        return data.ToArray();
    }

    private ReviewPhotoService CreatePhotoService(AppDbContext context) =>
        new(
            new VisitTokenRepository(context),
            _codec,
            new ReviewPhotoRepository(context),
            _storage,
            new SkiaImageProcessor(),
            CreateRemoval(context),
            _clock.Object);

    private ReviewPhotoRemoval CreateRemoval(AppDbContext context) =>
        new(new ReviewPhotoRepository(context), _storage, _clock.Object);

    private VerificationService CreateVerification(AppDbContext context) =>
        new(new VisitTokenRepository(context), new RestaurantRepository(context), _codec, _clock.Object, new ReviewPhotoRepository(context));

    private ReviewPhotoCleanupService CreateCleanup(AppDbContext context) =>
        new(new ReviewPhotoRepository(context), _storage, CreateRemoval(context), _clock.Object);

    private ReviewModerationService CreateModeration(AppDbContext context) =>
        new(
            new ReviewRepository(context),
            new ReviewCommentRepository(context),
            TestNotifications.Publisher(context, _clock.Object),
            _clock.Object,
            _audit,
            new ReviewPhotoRepository(context),
            CreateRemoval(context));

    private AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_databaseName).Options);
}
