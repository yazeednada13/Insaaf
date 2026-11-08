using System.Text.Json.Serialization;

namespace Insaaf.Application.Verification.Dtos;

/// <remarks><c>Token</c> may be the full QR payload ("insaaf://visit/…") or the bare token.</remarks>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ResolveVisitRequest(string? Token);

public sealed record VerifiedRestaurantDto(
    Guid Id,
    string Name,
    string City,
    string? Address,
    string? ImageUrl,
    decimal? AverageRating,
    int ReviewCount);

public sealed record ResolvedVisitDto(VerifiedRestaurantDto Restaurant);

/// <summary>
/// The only review-creation payload in the MVP. There is deliberately no restaurant field: the
/// restaurant comes from the visit token. Unknown members (e.g. "restaurantId") are rejected with 400.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateVerifiedReviewRequest(
    string? Token,
    int Rating,
    int FoodRating,
    int ServiceRating,
    int ValueRating,
    int AtmosphereRating,
    string? Text,
    IReadOnlyList<Guid>? PhotoIds = null);

public sealed record VerifiedReviewCreatedDto(Guid ReviewId, Guid RestaurantId);
