using System.Buffers.Text;
using System.Security.Cryptography;

namespace LnShelf.Api.Features.Calendar.Models;

/// <summary>
/// The secret in a release calendar's URL: 16 random bytes as unpadded base64url.
/// </summary>
// ShareToken's shape and reasoning (128 bits, checked before any query), kept as the
// Calendar slice's own type so the two secrets can change independently. The column is
// wider (CalendarFeed.MaximumTokenLength) only to leave room for that.
public static class CalendarToken
{
    /// <summary>16 bytes in base64url without padding.</summary>
    public const int Length = 22;

    public static string Create() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16));

    /// <summary>Whether a token from a URL could be one <see cref="Create"/> made.</summary>
    // Anything else is a 404 without a database call: the feed endpoint is anonymous.
    public static bool IsWellFormed(string? token) =>
        token is { Length: Length }
        && token.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');
}
