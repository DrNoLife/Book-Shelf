using System.Security.Cryptography;

namespace LnShelf.Api.Features.Covers.Models;

/// <summary>How a stored cover's bytes are fingerprinted for sharing.</summary>
public static class CoverContentHash
{
    /// <summary>Exactly this many lowercase hex characters: a SHA-256 digest.</summary>
    public const int Length = 64;

    public static string Compute(ReadOnlySpan<byte> content) =>
        Convert.ToHexStringLower(SHA256.HashData(content));
}
