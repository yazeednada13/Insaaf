using Insaaf.Application.Restaurants;
using Insaaf.Domain.Repositories.Projections;

namespace Insaaf.Application.Tests.Restaurants;

public sealed class OpeningHoursCalculatorTests
{
    // 2026-11-01 is a Sunday. "UTC" keeps local time equal to UTC on every OS.
    private static DateTime SundayUtc(int hour, int minute = 0) =>
        new(2026, 11, 1, hour, minute, 0, DateTimeKind.Utc);

    private static OpeningHoursProjection Period(DayOfWeek day, int opensHour, int closesHour) =>
        new(day, new TimeOnly(opensHour, 0), new TimeOnly(closesHour, 0));

    [Fact]
    public void Evaluate_ReturnsOpenWithTodayHours_WithinSameDayPeriod()
    {
        var status = OpeningHoursCalculator.Evaluate("UTC", [Period(DayOfWeek.Sunday, 12, 23)], SundayUtc(13));

        Assert.True(status.IsOpen);
        Assert.Equal("12:00 م — 11:00 م", status.TodayHours);
    }

    [Fact]
    public void Evaluate_ReturnsClosed_AtClosingTime()
    {
        var status = OpeningHoursCalculator.Evaluate("UTC", [Period(DayOfWeek.Sunday, 12, 23)], SundayUtc(23));

        Assert.False(status.IsOpen);
    }

    [Fact]
    public void Evaluate_UsesPreviousDayPeriod_AfterMidnight()
    {
        // Saturday 10:00 → Sunday 01:00.
        var periods = new[] { Period(DayOfWeek.Saturday, 10, 1) };

        var stillOpen = OpeningHoursCalculator.Evaluate("UTC", periods, SundayUtc(0, 30));
        var closed = OpeningHoursCalculator.Evaluate("UTC", periods, SundayUtc(1, 30));

        Assert.True(stillOpen.IsOpen);
        Assert.False(closed.IsOpen);
        // No periods start on Sunday itself.
        Assert.Null(stillOpen.TodayHours);
    }

    [Fact]
    public void Evaluate_JoinsSplitPeriodsInOpeningOrder()
    {
        var periods = new[] { Period(DayOfWeek.Sunday, 18, 23), Period(DayOfWeek.Sunday, 0, 3) };

        var status = OpeningHoursCalculator.Evaluate("UTC", periods, SundayUtc(15));

        Assert.False(status.IsOpen);
        Assert.Equal("12:00 ص — 3:00 ص، 6:00 م — 11:00 م", status.TodayHours);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Not/AZone")]
    public void Evaluate_ReturnsUnknown_WhenTimeZoneIsMissingOrInvalid(string? timeZoneId)
    {
        var status = OpeningHoursCalculator.Evaluate(timeZoneId, [Period(DayOfWeek.Sunday, 0, 23)], SundayUtc(12));

        Assert.Null(status.IsOpen);
        Assert.Null(status.TodayHours);
    }

    [Fact]
    public void Evaluate_ReturnsUnknown_WhenRestaurantHasNoHours()
    {
        var status = OpeningHoursCalculator.Evaluate("UTC", [], SundayUtc(12));

        Assert.Null(status.IsOpen);
        Assert.Null(status.TodayHours);
    }

    [Fact]
    public void LocalTimesFor_ResolvesAmmanAndSkipsUnknownZones()
    {
        var zones = OpeningHoursCalculator.LocalTimesFor(["Asia/Amman", "Not/AZone"], SundayUtc(12));

        var amman = Assert.Single(zones);
        Assert.Equal("Asia/Amman", amman.TimeZoneId);
    }
}
