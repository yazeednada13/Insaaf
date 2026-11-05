using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Insaaf.Application.Verification.Abstractions;

namespace Insaaf.Infrastructure.Verification;

/// <summary>Same primitives as refresh tokens: CSPRNG for the secret, SHA-256 for what is stored.</summary>
public sealed class VisitTokenCodec : IVisitTokenCodec
{
    private const int TokenBytes = 32;

    public string GenerateToken() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenBytes));

    public string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
