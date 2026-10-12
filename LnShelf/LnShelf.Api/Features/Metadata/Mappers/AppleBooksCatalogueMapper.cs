using System.Text.RegularExpressions;
using LnShelf.Api.Features.Metadata.Models.AppleBooks;

namespace LnShelf.Api.Features.Metadata.Mappers;

/// <summary>
/// Dirty iTunes search response → clean <see cref="CatalogueVolume"/> records. The
/// dirty→clean edge for Apple Books (CLAUDE.md — Mapping).
/// </summary>
// Partial only for the generated regex below.
public static partial class AppleBooksCatalogueMapper
{
    /// <summary>What Apple calls a book, as opposed to an audiobook or an app.</summary>
    private const string EbookKind = "ebook";

    /// <summary>The size segment in an <c>artworkUrl100</c>, and what to swap it for.</summary>
    // Measured: the size is a path segment the CDN honours as a fit-inside box, so
    // 1000x1500 on a 2:3 cover returns a real 1000x1500 JPEG rather than an upscale of the
    // thumbnail. A larger box gains nothing — CoverNormalizer caps the long edge at 1000
    // on ingest anyway — and a smaller one would land art on the shelf softer than the
    // grid renders it.
    private const string ThumbnailSize = "100x100bb";
    private const string CoverSize = "1000x1500bb";

    /// <summary>
    /// Every book in the response, in the store's own order.
    /// </summary>
    // Non-book rows are dropped here rather than by the caller, because "which rows are
    // books" is a fact about this payload rather than a conflict rule. Everything that
    // survives is still unfiltered as to *which* series it belongs to — that judgement is
    // VolumeEditionMatcher's, and it belongs to the caller (IVolumeCatalogueSource).
    public static IReadOnlyList<CatalogueVolume> ToCatalogueVolumes(AppleBooksSearchResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return
        [
            .. (response.Results ?? [])
                .Where(IsEbook)
                .Select(ToCatalogueVolume)
                .OfType<CatalogueVolume>(),
        ];
    }

    /// <returns>Null when the row carries no title, which leaves nothing to place it by.</returns>
    public static CatalogueVolume? ToCatalogueVolume(AppleBooksSearchResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.TrackName.NullIfBlank() is not { } title)
        {
            return null;
        }

        var (artworkUrl, artworkScreened) = ToArtwork(result.ArtworkUrl100);

        return new CatalogueVolume
        {
            SourceRecordKey = ToRecordKey(result),

            // Verbatim, deliberately. The matcher normalises it on the way in and the
            // caption prints it on the way out; trimming the "(light novel)" marker here
            // would throw away the only thing that positively states the medium.
            Title = title,

            // The blurb arrives as HTML — <br /> and &#xa0; both appear in real rows — and
            // nothing inward may hold markup, so it is cleaned at this edge.
            Description = HtmlDescriptionSanitizer.Sanitize(result.Description),

            ReleaseDate = ToReleaseDate(result.ReleaseDate),
            ArtworkUrl = artworkUrl,
            ArtworkScreened = artworkScreened,
            Genres = [.. (result.Genres ?? []).Select(genre => genre.NullIfBlank()).OfType<string>()],
        };
    }

    /// <summary>
    /// A book row, said positively.
    /// </summary>
    // Not "is not an audiobook": an audiobook row has no `kind` at all, so a negative test
    // would let it through.
    private static bool IsEbook(AppleBooksSearchResult result) =>
        string.Equals(result.Kind, EbookKind, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Apple's own track id, which is stable across every request for that listing.
    /// </summary>
    // The artwork URL is the fallback rather than the title: two printings of one volume
    // share a title, and this key exists to identify the record in a log line.
    private static string ToRecordKey(AppleBooksSearchResult result) =>
        result.TrackId is { } trackId
            ? trackId.ToString(CultureInfo.InvariantCulture)
            : result.ArtworkUrl100.NullIfBlank() ?? "unknown";

    /// <summary>
    /// The date part of the timestamp, read as text rather than converted.
    /// </summary>
    // "2021-08-24T07:00:00Z" is midnight on the 24th in the storefront's own timezone,
    // rendered in UTC. Parsing it as an instant and then asking a local clock for the day
    // would move a release into the 23rd or the 25th depending on where the container
    // runs; the ten characters Apple wrote are the answer it meant.
    private static DateOnly? ToReleaseDate(string? releaseDate)
    {
        const int IsoDateLength = 10;

        if (releaseDate is null || releaseDate.Length < IsoDateLength)
        {
            return null;
        }

        return DateOnly.TryParseExact(
            releaseDate[..IsoDateLength], "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
    }

    /// <summary>
    /// The same artwork at cover size, or exactly what Apple gave when it cannot be, or none
    /// when it is Apple's placeholder — and whether the placeholder was looked for at all.
    /// </summary>
    // The placeholder is recognised by its file name. Apple lists a book whose art is not
    // ready with a typographic stand-in, the title set in type on a plain field, and names
    // that file "coverArt.<hash>.png" (86 Vol. 14, Chainsaw Man Vol. 23, Solo Leveling:
    // Ragnarok Vol. 3); a real cover is named for its ISBN or by its publisher. Before this,
    // every cover on a book not out yet was refused for fear of the placeholder, which also
    // refused the real art publishers put up months ahead (Ragnarok Vol. 2).
    //
    // Only a URL of the usual shape, the file name followed by the size segment, can be read
    // that way. Any other shape is passed on unscreened, and the old date rule decides.
    private static (Uri? Url, bool Screened) ToArtwork(string? artworkUrl100)
    {
        if (artworkUrl100.NullIfBlank() is not { } url)
        {
            return (null, false);
        }

        if (ArtworkFileName(url) is not { } fileName)
        {
            // Guarded rather than assumed: the swap is a string edit on a URL shape Apple owns,
            // and a row that does not carry that shape still has usable art at whatever size
            // it came in.
            return (Uri.TryCreate(url, UriKind.Absolute, out var asGiven) ? asGiven : null, false);
        }

        if (PlaceholderFileNamePattern().IsMatch(fileName))
        {
            return (null, true);
        }

        var upgraded = url.Replace(ThumbnailSize, CoverSize, StringComparison.Ordinal);

        return (Uri.TryCreate(upgraded, UriKind.Absolute, out var uri) ? uri : null, true);
    }

    /// <summary>
    /// The file name in ".../9781975320775.jpg/100x100bb.jpg": the path segment before the size.
    /// </summary>
    /// <returns>Null when the URL is not of that shape.</returns>
    private static string? ArtworkFileName(string url)
    {
        var segments = url.Split('/');

        return segments.Length >= 2 && segments[^1].StartsWith(ThumbnailSize, StringComparison.Ordinal)
            ? segments[^2].NullIfBlank()
            : null;
    }

    /// <summary>"coverArt.faef0f.png": the stand-in Apple lists before a book's art is ready.</summary>
    // A png on purpose: every measured placeholder is one, while "coverArt.ddf028.jpg" turned
    // up as the real, if plain, cover of a public-domain book from 1912.
    [GeneratedRegex(@"^coverArt\.[0-9a-f]+\.png$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex PlaceholderFileNamePattern();
}
