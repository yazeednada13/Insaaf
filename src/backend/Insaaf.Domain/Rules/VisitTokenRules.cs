using Insaaf.Domain.Entities;

namespace Insaaf.Domain.Rules;

public enum VisitTokenState
{
    Valid,
    Expired,
    Used,
    Revoked,
}

/// <summary>Single source of truth for whether a visit token can be redeemed, and by which restaurant.</summary>
public static class VisitTokenRules
{
    /// <summary>
    /// A redeemed token is reported as used even if it later expired or was revoked; a token is
    /// expired at <c>ExpiresAt</c> itself.
    /// </summary>
    public static VisitTokenState Evaluate(VisitToken token, DateTime utcNow)
    {
        if (token.RedeemedAt is not null)
        {
            return VisitTokenState.Used;
        }

        if (token.RevokedAt is not null)
        {
            return VisitTokenState.Revoked;
        }

        return utcNow >= token.ExpiresAt ? VisitTokenState.Expired : VisitTokenState.Valid;
    }

    /// <summary>Only active partner restaurants may have tokens issued, resolved or redeemed.</summary>
    public static bool IsEligibleRestaurant(bool isActive, bool isPartner) => isActive && isPartner;
}
