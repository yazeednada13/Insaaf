namespace Insaaf.Domain.Repositories.Projections;

public sealed record OpeningHoursProjection(DayOfWeek DayOfWeek, TimeOnly OpensAt, TimeOnly ClosesAt);
