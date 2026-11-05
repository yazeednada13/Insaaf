namespace Insaaf.Application.Verification.Dtos;

/// <summary>Channel-independent issuance request (the caller decides count and expiry).</summary>
public sealed record VisitTokenIssueRequest(Guid RestaurantId, int Count, DateTime ExpiresAt);

/// <remarks><c>Payloads</c> contain the secret tokens and are returned only once, at issuance.</remarks>
public sealed record IssuedVisitTokensDto(
    Guid IssuanceId,
    Guid RestaurantId,
    DateTime ExpiresAt,
    IReadOnlyList<string> Payloads);

public sealed record VisitTokenSummaryDto(int Active, int Redeemed, int Expired, int Revoked);

public sealed record RevokedVisitTokensDto(int RevokedCount);
