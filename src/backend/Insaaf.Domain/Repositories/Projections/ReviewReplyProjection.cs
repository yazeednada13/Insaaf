namespace Insaaf.Domain.Repositories.Projections;

/// <summary>The restaurant's official reply to a review (Phase 11).</summary>
public sealed record ReviewReplyProjection(string Text, DateTime CreatedAt, DateTime UpdatedAt);
