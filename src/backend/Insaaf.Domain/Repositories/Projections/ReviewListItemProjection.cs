namespace Insaaf.Domain.Repositories.Projections;

public sealed record ReviewListItemProjection(
    Guid Id,
    int Rating,
    string Text,
    DateTime CreatedAt,
    bool IsVerified,
    Guid ReviewerId,
    string? ReviewerFullName,
    string? ReviewerUserName);
