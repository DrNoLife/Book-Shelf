namespace LnShelf.Api.Features.Covers.Models;

/// <summary>
/// Builds and validates cover object keys. One place, because the key is both
/// persisted and accepted from a URL.
/// </summary>
public static class CoverObjectKey
{
    /// <summary>
    /// Keyed on the AniList media id, so a cover cached while reviewing an import
    /// candidate is already the series' cover the moment that candidate is chosen.
    /// </summary>
    public static string ForAniList(int aniListId, string extension) =>
        $"anilist/{aniListId}{extension}";

    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];

    /// <summary>The extension of a source URL, defaulting to jpeg — AniList's usual.</summary>
    public static string ExtensionFor(Uri sourceUri)
    {
        ArgumentNullException.ThrowIfNull(sourceUri);

        var extension = Path.GetExtension(sourceUri.AbsolutePath).ToLowerInvariant();
        return AllowedExtensions.Contains(extension) ? extension : ".jpg";
    }

    /// <summary>
    /// Whether a key from a request URL is safe to hand to the store.
    /// </summary>
    // The cover route is a catch-all, so the key is untrusted input. Anything that
    // could climb out of the prefix is refused outright rather than normalised —
    // normalising attacker input is how traversal bugs survive.
    public static bool IsWellFormed(string? objectKey) =>
        !string.IsNullOrWhiteSpace(objectKey)
        && objectKey.Length <= 300
        && !objectKey.Contains("..", StringComparison.Ordinal)
        && !objectKey.StartsWith('/')
        && !objectKey.Contains('\\', StringComparison.Ordinal)
        && objectKey.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '/' or '-' or '_' or '.');
}
