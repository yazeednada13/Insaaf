using Insaaf.Domain.Repositories.Projections;

namespace Insaaf.Domain.Rules;

/// <summary>
/// Opening-hours rule, evaluated in the restaurant's local time.
/// A period on <c>day</c> covers [OpensAt, ClosesAt); when ClosesAt &lt;= OpensAt it continues past
/// midnight, so a period from the previous day can still be open early this morning.
/// RestaurantRepository mirrors this rule in LINQ for the openNow filter; keep them in sync.
/// </summary>
public static class OpeningHoursRules
{
    public static bool IsOpenAt(
        IEnumerable<OpeningHoursProjection> periods,
        DayOfWeek day,
        TimeOnly time)
    {
        var previousDay = PreviousDay(day);

        return periods.Any(period =>
            (period.DayOfWeek == day
                && period.OpensAt <= time
                && (period.ClosesAt <= period.OpensAt || time < period.ClosesAt))
            || (period.DayOfWeek == previousDay
                && period.ClosesAt <= period.OpensAt
                && time < period.ClosesAt));
    }

    public static DayOfWeek PreviousDay(DayOfWeek day) => (DayOfWeek)(((int)day + 6) % 7);
}
