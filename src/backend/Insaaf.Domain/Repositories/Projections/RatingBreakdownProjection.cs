namespace Insaaf.Domain.Repositories.Projections;

/// <summary>Unrounded averages of the detailed ratings over a restaurant's public reviews.</summary>
public sealed record RatingBreakdownProjection(
    decimal Food,
    decimal Service,
    decimal Value,
    decimal Atmosphere);
