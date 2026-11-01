namespace Insaaf.Application.Reviews;

public static class RatingMath
{
    /// <summary>Display rounding for rating averages: one decimal place, halves away from zero (4.25 → 4.3).</summary>
    public static decimal? RoundAverage(decimal? average) =>
        average is null ? null : Math.Round(average.Value, 1, MidpointRounding.AwayFromZero);

    public static decimal RoundAverage(decimal average) =>
        Math.Round(average, 1, MidpointRounding.AwayFromZero);
}
