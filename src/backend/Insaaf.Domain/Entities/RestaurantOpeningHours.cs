namespace Insaaf.Domain.Entities;

/// <summary>
/// One opening period in the restaurant's local time. A day may have several periods;
/// a day without periods is closed. When <see cref="ClosesAt"/> is not after <see cref="OpensAt"/>,
/// the period runs past midnight into the next day (e.g. 10:00 → 01:00).
/// </summary>
public sealed class RestaurantOpeningHours
{
    public Guid Id { get; set; }

    public Guid RestaurantId { get; set; }

    public Restaurant Restaurant { get; set; } = null!;

    public DayOfWeek DayOfWeek { get; set; }

    public TimeOnly OpensAt { get; set; }

    public TimeOnly ClosesAt { get; set; }
}
