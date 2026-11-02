namespace Insaaf.Domain.Enums;

/// <summary>How a visit token was issued. Phase 5 issues only <see cref="Manual"/> tokens.</summary>
public enum VisitTokenIssuanceChannel
{
    /// <summary>Issued by an Admin and distributed through the pilot's manual process.</summary>
    Manual = 1,

    /// <summary>Reserved for a future restaurant POS integration (not implemented).</summary>
    Pos = 2,
}
