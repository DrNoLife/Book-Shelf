namespace LnShelf.Api.Features.Covers.Models;

/// <summary>
/// Builds and validates cover object keys. One place, because the key is both
/// persisted and accepted from a URL.
/// </summary>
public static class CoverObjectKey
{
    /// <summary>
    /// A series' cover from one series-level source, keyed on that source's own id.
    /// </summary>
    // The source is in the key for the reason ForVolumeSync gives: two sources' art for
    // one series must not land on the same file. The id is a long because MangaUpdates'
    // are eleven digits.
    //
    // For AniList this produces exactly the key it always did — `anilist/85470.jpg` — so
    // nothing already in the store is orphaned by this becoming the general form.
    public static string ForSeriesSource(MetadataSourceName source, long sourceId, string extension) =>
        $"{Slug(source.ToString())}/{sourceId}{extension}";

    /// <summary>
    /// A user-supplied cover for one volume, from one upload.
    /// </summary>
    /// <param name="uploadId">Fresh per upload; the caller deletes the key it replaces.</param>
    // A separate prefix from the synced art on purpose: "overrides always win and are
    // never replaced by sync" (CLAUDE.md) is easier to keep true when sync cannot even
    // name the key.
    //
    // A new key per upload, not one per volume. The cover endpoint tells browsers to keep
    // a cover for a week as immutable, on the promise that the key changes when the art
    // does — a fixed key broke that promise for exactly one kind of cover, and a replaced
    // override kept showing the old picture for up to a week.
    public static string ForVolumeOverride(Guid volumeId, Guid uploadId, string extension) =>
        $"override/volume/{volumeId}/{uploadId:N}{extension}";

    /// <summary>
    /// Synced art for one volume, from one source.
    /// </summary>
    // A different prefix from ForVolumeOverride, which is the mechanism behind
    // "overrides always win and sync never replaces one" (CLAUDE.md): sync cannot even
    // name an override's key, so it cannot overwrite one by accident. The source is in
    // the key so a second enrichment source's art does not land on top of the first's.
    public static string ForVolumeSync(Guid volumeId, string sourceName, string extension) =>
        $"volume/{Slug(sourceName)}/{volumeId}{extension}";

    /// <summary>Where every avatar lives: a provider picture's copy or an uploaded one.</summary>
    public const string AvatarPrefix = "avatar/";

    /// <summary>
    /// One user's avatar, from one fetch or upload. Always webp: avatars are only ever stored
    /// through the normalizer.
    /// </summary>
    /// <param name="uploadId">Fresh per picture; the caller deletes the key it replaces.</param>
    // A new key per picture for the reason ForVolumeOverride gives, and the file name doubles
    // as the avatar's version: the client puts it in the URL so a changed picture shows at once.
    // The user id is in the key so account deletion and the orphan sweep can tell whose it is.
    public static string ForAvatar(Guid userId, Guid uploadId) =>
        $"{AvatarPrefix}{userId}/{uploadId:N}{CoverNormalizer.Extension}";

    /// <summary>The user an avatar key belongs to, or null for any other key.</summary>
    public static Guid? AvatarUserIdOf(string objectKey)
    {
        ArgumentNullException.ThrowIfNull(objectKey);

        return objectKey.Split('/') is ["avatar", var userId, var file]
               && Guid.TryParse(userId, out var parsed)
               && Guid.TryParseExact(Path.GetFileNameWithoutExtension(file), "N", out _)
            ? parsed
            : null;
    }

    /// <summary>
    /// The version a client puts in the avatar URL: the picture's own upload id, which changes
    /// whenever the picture does. Null when there is no picture.
    /// </summary>
    public static string? AvatarVersionOf(string? objectKey) =>
        objectKey is null ? null : Path.GetFileNameWithoutExtension(objectKey);

    /// <summary>
    /// The volume a per-volume key belongs to (an override or a volume's synced art), or
    /// null for a key shared by every copy of a series (a source's series art).
    /// </summary>
    // The covers endpoint and account deletion both need to know whose a key is, and the
    // volume id is the only owner the key carries.
    public static Guid? VolumeIdOf(string objectKey)
    {
        ArgumentNullException.ThrowIfNull(objectKey);

        var parts = objectKey.Split('/');
        var candidate = parts switch
        {
            ["override", "volume", var volumeId, _] => volumeId,
            ["volume", _, var file] => Path.GetFileNameWithoutExtension(file),
            _ => null,
        };

        return Guid.TryParse(candidate, out var parsed) ? parsed : null;
    }

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
