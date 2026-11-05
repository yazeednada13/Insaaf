namespace Insaaf.Application.Verification.Abstractions;

/// <summary>Creates unguessable visit tokens and the one-way hash that is stored instead of them.</summary>
public interface IVisitTokenCodec
{
    /// <summary>A new random token (256 bits, base64url, 43 characters).</summary>
    string GenerateToken();

    /// <summary>Deterministic SHA-256 hex hash used for storage and lookup.</summary>
    string Hash(string token);
}
