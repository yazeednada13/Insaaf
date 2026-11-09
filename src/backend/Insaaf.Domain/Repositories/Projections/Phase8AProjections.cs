using Insaaf.Domain.Enums;

namespace Insaaf.Domain.Repositories.Projections;

public sealed record NotificationProjection(
    Guid Id,
    NotificationType Type,
    Guid ActorId,
    string? ActorFullName,
    string? ActorUserName,
    bool ActorHasPublicProfile,
    Guid? ReviewId,
    DateTime CreatedAt,
    DateTime? ReadAt,
    string? ActorAvatarKey = null,
    Guid? RestaurantId = null,
    string? RestaurantName = null);

public sealed record CategoryTasteProjection(string Name, string Slug, int ReviewCount, double AverageRating);

public sealed record TasteProjection(int ReviewCount, IReadOnlyList<CategoryTasteProjection> Categories);
