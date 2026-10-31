using System.Globalization;
using Insaaf.Domain.Repositories;
using Insaaf.Domain.Repositories.Projections;
using Insaaf.Domain.Rules;

namespace Insaaf.Application.Restaurants;

public sealed record OpeningStatus(bool? IsOpen, string? TodayHours);

/// <summary>
/// Evaluates opening hours in the restaurant's own time zone. Status is unknown (null) when the
/// restaurant has no time zone, an unrecognised time zone, or no opening hours at all.
/// </summary>
public static class OpeningHoursCalculator
{
    public static OpeningStatus Evaluate(
        string? timeZoneId,
        IReadOnlyList<OpeningHoursProjection> periods,
        DateTime utcNow)
    {
        if (periods.Count == 0 || TryGetLocalTime(timeZoneId, utcNow) is not { } local)
        {
            return new OpeningStatus(null, null);
        }

        var isOpen = OpeningHoursRules.IsOpenAt(periods, local.Day, local.Time);
        var todayPeriods = periods
            .Where(period => period.DayOfWeek == local.Day)
            .OrderBy(period => period.OpensAt)
            .Select(period => $"{FormatTime(period.OpensAt)} — {FormatTime(period.ClosesAt)}")
            .ToList();

        return new OpeningStatus(isOpen, todayPeriods.Count == 0 ? null : string.Join("، ", todayPeriods));
    }

    /// <summary>The local day and time in each time zone, for the repository openNow filter.</summary>
    public static IReadOnlyList<LocalTimeInZone> LocalTimesFor(
        IEnumerable<string> timeZoneIds,
        DateTime utcNow) =>
        timeZoneIds
            .Select(timeZoneId => TryGetLocalTime(timeZoneId, utcNow))
            .OfType<LocalTimeInZone>()
            .ToList();

    private static LocalTimeInZone? TryGetLocalTime(string? timeZoneId, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId)
            || !TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var timeZone))
        {
            return null;
        }

        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), timeZone);
        return new LocalTimeInZone(timeZoneId, local.DayOfWeek, TimeOnly.FromDateTime(local));
    }

    // Figma format: "11:00 ص", "1:00 م" (12-hour clock, ص = AM, م = PM).
    private static string FormatTime(TimeOnly time)
    {
        var hour = time.Hour % 12 == 0 ? 12 : time.Hour % 12;
        var period = time.Hour < 12 ? "ص" : "م";
        return $"{hour.ToString(CultureInfo.InvariantCulture)}:{time.Minute:00} {period}";
    }
}
