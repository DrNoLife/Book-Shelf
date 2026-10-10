using System.Buffers.Text;
using System.Security.Cryptography;

namespace LnShelf.Api.Features.Sharing.Models;

/// <summary>
/// The secret in a shared shelf's link: 16 random bytes as unpadded base64url.
/// </summary>
// 128 bits, so guessing one is not an attack worth defending against beyond the rate limit.
// A collision (2^-128) is not retried: the unique index would surface it as a 500.
public static class ShareToken
{
    /// <summary>16 bytes in base64url without padding.</summary>
    public const int Length = 22;

    public static string Create() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16));

    /// <summary>Whether a token from a URL could be one <see cref="Create"/> made.</summary>
    // Checked before any database call: anything that is not exactly what Create makes is a
    // 404 without a query, so the anonymous endpoint never sends arbitrary input to SQL.
    public static bool IsWellFormed(string? token) =>
        token is { Length: Length }
        && token.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');
}
