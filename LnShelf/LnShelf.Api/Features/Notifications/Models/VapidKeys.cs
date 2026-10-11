using System.Buffers.Text;
using System.Security.Cryptography;

namespace LnShelf.Api.Features.Notifications.Models;

/// <summary>
/// The server's Web Push identity (VAPID, RFC 8292): a P-256 key pair as unpadded base64url,
/// the shape browsers and Lib.Net.Http.WebPush both take.
/// </summary>
/// <param name="PublicKey">The uncompressed point, 65 bytes: 0x04, then X and Y.</param>
/// <param name="PrivateKey">The private scalar, 32 bytes.</param>
public sealed record VapidKeys(string PublicKey, string PrivateKey)
{
    /// <summary>An uncompressed P-256 point: the 0x04 marker and two 32-byte coordinates.</summary>
    public const int PublicKeyLength = 65;

    public const int PrivateKeyLength = 32;

    /// <summary>A browser's push authentication secret.</summary>
    public const int AuthenticationSecretLength = 16;

    /// <summary>A fresh pair, for <c>vapid-keys</c>.</summary>
    public static VapidKeys Create()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var parameters = key.ExportParameters(includePrivateParameters: true);

        byte[] publicKey = [0x04, .. parameters.Q.X!, .. parameters.Q.Y!];

        return new VapidKeys(Base64Url.EncodeToString(publicKey), Base64Url.EncodeToString(parameters.D));
    }

    /// <summary>Whether a value is an uncompressed P-256 public key in base64url, on the curve.</summary>
    public static bool IsPublicKey(string? value) =>
        TryDecode(value, PublicKeyLength) is [0x04, ..] bytes && IsOnCurve(bytes);

    // The length and marker alone let through 65 bytes that are no point at all; such a key
    // only fails later, when a push is encrypted for it. Importing it is the curve check.
    private static bool IsOnCurve(byte[] point)
    {
        try
        {
            using var key = ECDiffieHellman.Create(new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint { X = point[1..33], Y = point[33..] },
            });

            return true;
        }
        // Windows (CNG) reports an off-curve point as an unsupported curve; Linux (OpenSSL),
        // where the API runs, as a cryptographic error.
        catch (Exception exception) when (exception is CryptographicException or PlatformNotSupportedException)
        {
            return false;
        }
    }

    /// <summary>Whether a value decodes to exactly <paramref name="byteCount"/> bytes of base64url.</summary>
    public static bool DecodesTo(string? value, int byteCount) => TryDecode(value, byteCount) is not null;

    private static byte[]? TryDecode(string? value, int byteCount)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 2 * byteCount)
        {
            return null;
        }

        try
        {
            var bytes = Base64Url.DecodeFromChars(value.Trim());
            return bytes.Length == byteCount ? bytes : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
