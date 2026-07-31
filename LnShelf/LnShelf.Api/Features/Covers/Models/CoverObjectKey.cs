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

    /// <summary>
    /// A user-supplied cover for one volume. Keyed on the volume, so an override
    /// replaces itself rather than accumulating files.
    /// </summary>
    // A separate prefix from the synced art on purpose: "overrides always win and are
    // never replaced by sync" (CLAUDE.md) is easier to keep true when sync cannot even
    // name the key.
    public static string ForVolumeOverride(Guid volumeId, string extension) =>
        $"override/volume/{volumeId}{extension}";

    /// <summary>
    /// Synced art for one volume, from one source.
    /// </summary>
    // A different prefix from ForVolumeOverride, which is the mechanism behind
    // "overrides always win and sync never replaces one" (CLAUDE.md): sync cannot even
    // name an override's key, so it cannot overwrite one by accident. The source is in
    // the key so a second enrichment source's art does not land on top of the first's.
    public static string ForVolumeSync(Guid volumeId, string sourceName, string extension) =>
        $"volume/{Slug(sourceName)}/{volumeId}{extension}";

    /// <summary>
    /// A source name reduced to what <see cref="IsWellFormed"/> allows.
    /// </summary>
    // The source name reaches this from a SourceName property, so it is not attacker
    // input — but it is going into a key that IsWellFormed later has to accept, and a
    // space or a dot in it would make a key the cover endpoint refuses to serve.
    private static string Slug(string sourceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);

        var slug = new StringBuilder(sourceName.Length);

        foreach (var character in sourceName.ToLowerInvariant())
        {
            slug.Append(char.IsAsciiLetterOrDigit(character) ? character : '-');
        }

        return slug.ToString();
    }

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
