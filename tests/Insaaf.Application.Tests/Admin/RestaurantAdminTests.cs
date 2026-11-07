using Insaaf.Application.Admin;
using Insaaf.Application.Admin.Dtos;
using Insaaf.Application.Auth.Abstractions;
using Insaaf.Application.Restaurants;
using Insaaf.Application.Restaurants.Dtos;
using Insaaf.Domain.Entities;
using Insaaf.Domain.Enums;
using Insaaf.Domain.Repositories.Projections;
using Insaaf.Infrastructure.Persistence;
using Insaaf.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Insaaf.Application.Tests.Admin;

/// <summary>Phase 9, task 9.1: Admin restaurant management, its validation and its audit lines.</summary>
public sealed class RestaurantAdminTests
{
    private static readonly DateTime Now = new(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _databaseName = $"admin-restaurants-{Guid.NewGuid():N}";
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly RecordingAuditLog _audit = new();
    private readonly Guid _adminId = Guid.NewGuid();
    private readonly Guid _burgerId = Guid.NewGuid();
    private readonly Guid _grillId = Guid.NewGuid();

    public RestaurantAdminTests()
    {
        _clock.Setup(clock => clock.UtcNow).Returns(Now);
        using var context = CreateContext();
        context.Categories.AddRange(
            new Category { Id = _burgerId, Name = "برجر", Slug = "burger" },
            new Category { Id = _grillId, Name = "مشاوي", Slug = "grill" });
        context.SaveChanges();
    }

    // ── Opening hours ─────────────────────────────────────────────────────────

    [Fact]
    public void Hours_OverlapOnTheSameDay_AreRefused() =>
        AssertInvalid(Valid() with { OpeningHours = [Hours(0, "10:00", "15:00"), Hours(0, "14:00", "20:00")] }, "overlap");

    [Fact]
    public void Hours_OvernightRunningIntoNextDay_AreRefused() =>
        // Sunday 22:00–02:00 and Monday 01:00–03:00 overlap from 01:00 to 02:00 on Monday.
        AssertInvalid(Valid() with { OpeningHours = [Hours(0, "22:00", "02:00"), Hours(1, "01:00", "03:00")] }, "overlap");

    [Fact]
    public void Hours_SaturdayNightWrappingIntoSunday_AreRefused() =>
        AssertInvalid(Valid() with { OpeningHours = [Hours(6, "20:00", "03:00"), Hours(0, "02:00", "06:00")] }, "overlap");

    [Fact]
    public void Hours_ThatOnlyTouch_AreAccepted() =>
        AssertValid(Valid() with { OpeningHours = [Hours(0, "22:00", "02:00"), Hours(1, "02:00", "05:00")] });

    [Fact]
    public void Hours_OvernightAndSeveralPerDay_AreAccepted() =>
        AssertValid(Valid() with
        {
            OpeningHours = [Hours(5, "12:00", "15:00"), Hours(5, "18:00", "02:00"), Hours(6, "12:00", "23:00")],
        });

    [Fact]
    public void Hours_OpeningAndClosingAtTheSameTime_AreRefused() =>
        AssertInvalid(Valid() with { OpeningHours = [Hours(2, "09:00", "09:00")] }, "same time");

    [Theory]
    [InlineData(7, "09:00", "17:00")]
    [InlineData(-1, "09:00", "17:00")]
    [InlineData(1, "9:00", "17:00")]
    [InlineData(1, "09:00", "25:00")]
    [InlineData(1, null, "17:00")]
    public void Hours_WithABadDayOrTime_AreRefused(int day, string? opensAt, string? closesAt) =>
        AssertInvalid(Valid() with { OpeningHours = [new OpeningHoursInput(day, opensAt, closesAt)] }, "HH:mm");

    [Fact]
    public void Hours_ExplicitlyEmpty_MeanUnknownAndAllowNoTimeZone() =>
        AssertValid(Valid() with { OpeningHours = [], TimeZoneId = null });

    // ── Time zone ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Asia/Amman")]
    [InlineData("Europe/London")]
    public void TimeZone_IanaIds_AreAccepted(string id) => AssertValid(Valid() with { TimeZoneId = id });

    [Theory]
    [InlineData("Arab Standard Time")]
    [InlineData("Mars/Phobos")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a zone")]
    public void TimeZone_WindowsIdsAndUnknownValues_AreRefused(string id) =>
        AssertInvalid(Valid() with { TimeZoneId = id }, "IANA");

    [Fact]
    public void TimeZone_MissingWhileHoursAreGiven_IsRefused() =>
        AssertInvalid(Valid() with { TimeZoneId = null }, "Time zone is required");

    // ── Full PUT body ─────────────────────────────────────────────────────────

    [Fact]
    public void Body_WithoutCategorySlugs_IsRefused() => AssertInvalid(Valid() with { CategorySlugs = null }, "Category slugs are required");

    [Fact]
    public void Body_WithoutOpeningHours_IsRefused() => AssertInvalid(Valid() with { OpeningHours = null }, "Opening hours are required");

    [Fact]
    public void Body_WithEmptyOrRepeatedCategories_IsRefused()
    {
        AssertInvalid(Valid() with { CategorySlugs = [] }, "category slugs are required");
        AssertInvalid(Valid() with { CategorySlugs = ["burger", "BURGER"] }, "repeat");
    }

    [Fact]
    public void Body_WithMissingOrBadRequiredFields_IsRefused()
    {
        AssertInvalid(Valid() with { Name = "  " }, "Name");
        AssertInvalid(Valid() with { City = null }, "City");
        AssertInvalid(Valid() with { Latitude = 91 }, "Latitude");
        AssertInvalid(Valid() with { Longitude = null }, "Longitude");
        AssertInvalid(Valid() with { PriceLevel = 5 }, "Price level");
        AssertInvalid(Valid() with { ImageUrl = "ftp://x/y.png" }, "Image URL");
    }

    // ── Service on the real repository ────────────────────────────────────────

    [Fact]
    public async Task Create_StoresTheRestaurantWithCategoriesHoursAndTimeZone()
    {
        await using var context = CreateContext();

        var created = await CreateService(context).CreateAsync(_adminId, Valid());

        Assert.True(created.IsActive);
        Assert.False(created.IsPartner);
        Assert.Equal("Asia/Amman", created.TimeZoneId);
        Assert.Equal(["burger"], created.Categories.Select(category => category.Slug));
        Assert.Equal(2, created.OpeningHours.Count);
        Assert.Equal(new OpeningHoursDto(5, "18:00", "02:00"), created.OpeningHours[1]);
        Assert.Equal(new AdminAuditEntry("restaurant.create", _adminId, "restaurant", created.Id, Now, AdminAuditResult.Succeeded), _audit.Entries.Single());
    }

    [Fact]
    public async Task Create_WithAnUnknownCategory_IsRefusedAndNothingIsStored()
    {
        await using var context = CreateContext();

        var error = await Assert.ThrowsAsync<AdminException>(() =>
            CreateService(context).CreateAsync(_adminId, Valid() with { CategorySlugs = ["burger", "sushi"] }));

        Assert.Equal("validation_error", error.Code);
        Assert.Contains("sushi", error.Message);
        Assert.Empty(await context.Restaurants.ToListAsync());
        Assert.Equal(AdminAuditResult.ValidationFailed, _audit.Entries.Single().Result);
    }

    [Fact]
    public async Task Update_ReplacesCategoriesAndHoursAsSets()
    {
        await using var context = CreateContext();
        var service = CreateService(context);
        var created = await service.CreateAsync(_adminId, Valid());

        var updated = await service.UpdateAsync(_adminId, created.Id, Valid() with
        {
            Name = "اسم جديد",
            CategorySlugs = ["grill", "burger"],
            OpeningHours = [Hours(1, "08:00", "12:00")],
        });

        Assert.Equal("اسم جديد", updated.Name);
        Assert.Equal(["burger", "grill"], updated.Categories.Select(category => category.Slug).Order());
        Assert.Equal([new OpeningHoursDto(1, "08:00", "12:00")], updated.OpeningHours);
        Assert.Single(await context.RestaurantOpeningHours.ToListAsync());
    }

    [Fact]
    public async Task Update_WithExplicitlyEmptyHours_ClearsThem()
    {
        await using var context = CreateContext();
        var service = CreateService(context);
        var created = await service.CreateAsync(_adminId, Valid());

        var updated = await service.UpdateAsync(_adminId, created.Id, Valid() with { OpeningHours = [], TimeZoneId = null });

        Assert.Empty(updated.OpeningHours);
        Assert.Null(updated.TimeZoneId);
    }

    [Fact]
    public async Task Update_WithAnOmittedField_IsRefusedAndKeepsExistingData()
    {
        await using var context = CreateContext();
        var service = CreateService(context);
        var created = await service.CreateAsync(_adminId, Valid());

        await Assert.ThrowsAsync<AdminException>(() =>
            service.UpdateAsync(_adminId, created.Id, Valid() with { Name = "تغيير", OpeningHours = null }));
        await Assert.ThrowsAsync<AdminException>(() =>
            service.UpdateAsync(_adminId, created.Id, Valid() with { Name = "تغيير", CategorySlugs = null }));

        await using var check = CreateContext();
        var stored = await new RestaurantRepository(check).GetForAdminAsync(created.Id);
        Assert.Equal(created.Name, stored!.Name);
        Assert.Equal(2, stored.OpeningHours.Count);
        Assert.Single(stored.Categories);
    }

    [Fact]
    public async Task Update_OfAMissingRestaurant_IsNotFound()
    {
        await using var context = CreateContext();

        var error = await Assert.ThrowsAsync<AdminException>(() =>
            CreateService(context).UpdateAsync(_adminId, Guid.NewGuid(), Valid()));

        Assert.Equal("not_found", error.Code);
        Assert.Equal(AdminAuditResult.NotFound, _audit.Entries.Single().Result);
    }

    [Fact]
    public async Task Deactivate_HidesTheRestaurantFromPublicReads_AndAdminStillSeesIt()
    {
        await using var context = CreateContext();
        var service = CreateService(context);
        var created = await service.CreateAsync(_adminId, Valid());
        var publicService = new RestaurantService(new RestaurantRepository(context), _clock.Object);

        var deactivated = await service.SetStatusAsync(_adminId, created.Id, new SetRestaurantStatusRequest(false));
        var again = await service.SetStatusAsync(_adminId, created.Id, new SetRestaurantStatusRequest(false));

        Assert.False(deactivated.IsActive);
        Assert.False(again.IsActive);
        Assert.Equal(AdminAuditResult.NoChange, _audit.Entries.Last().Result);
        var publicError = await Assert.ThrowsAsync<RestaurantException>(() => publicService.GetByIdAsync(created.Id));
        Assert.Equal("not_found", publicError.Code);
        Assert.Empty((await publicService.SearchAsync(new RestaurantSearchQuery())).Items);

        var all = await service.ListAsync(new AdminRestaurantListQuery());
        var activeOnly = await service.ListAsync(new AdminRestaurantListQuery(IncludeInactive: false));
        Assert.Single(all.Items);
        Assert.Empty(activeOnly.Items);

        var reactivated = await service.SetStatusAsync(_adminId, created.Id, new SetRestaurantStatusRequest(true));
        Assert.True(reactivated.IsActive);
        Assert.Single((await publicService.SearchAsync(new RestaurantSearchQuery())).Items);
    }

    [Fact]
    public async Task Status_WithoutIsActive_IsAValidationError()
    {
        await using var context = CreateContext();

        var error = await Assert.ThrowsAsync<AdminException>(() =>
            CreateService(context).SetStatusAsync(_adminId, Guid.NewGuid(), new SetRestaurantStatusRequest(null)));

        Assert.Equal("validation_error", error.Code);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static UpsertRestaurantRequest Valid() =>
        new(
            Name: "مطعم الاختبار",
            Description: "وصف",
            City: "Amman",
            Address: "شارع الجامعة",
            Latitude: 31.95,
            Longitude: 35.91,
            PriceLevel: 2,
            Phone: "+962790000000",
            ImageUrl: "https://example.com/image.jpg",
            TimeZoneId: "Asia/Amman",
            CategorySlugs: ["burger"],
            OpeningHours: [Hours(0, "12:00", "23:00"), Hours(5, "18:00", "02:00")]);

    private static OpeningHoursInput Hours(int day, string opensAt, string closesAt) => new(day, opensAt, closesAt);

    private static void AssertValid(UpsertRestaurantRequest request)
    {
        var (input, errors) = RestaurantInputValidator.Validate(request);
        Assert.Empty(errors);
        Assert.NotNull(input);
    }

    private static void AssertInvalid(UpsertRestaurantRequest request, string expected)
    {
        var (input, errors) = RestaurantInputValidator.Validate(request);
        Assert.Null(input);
        Assert.Contains(errors, error => error.Contains(expected, StringComparison.OrdinalIgnoreCase));
    }

    private RestaurantAdminService CreateService(AppDbContext context) =>
        new(new RestaurantRepository(context), _clock.Object, _audit);

    private AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_databaseName).Options);
}

/// <summary>Keeps audit entries in memory so tests can check what an Admin action recorded.</summary>
public sealed class RecordingAuditLog : IAdminAuditLog
{
    public List<AdminAuditEntry> Entries { get; } = [];

    public void Record(AdminAuditEntry entry) => Entries.Add(entry);
}
