namespace Insaaf.Domain.Repositories.Projections;

/// <summary>A Ready photo of a review, in its review order (Phase 9A).</summary>
public sealed record ReviewPhotoProjection(Guid Id, int Width, int Height);
