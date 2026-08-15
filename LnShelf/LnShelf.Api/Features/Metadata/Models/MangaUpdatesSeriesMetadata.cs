namespace LnShelf.Api.Features.Metadata.Models;

/// <summary>
/// Everything MangaUpdates knows about one series, in the shelf's terms — enough to
/// add it without AniList having been reachable at all.
/// </summary>
// A sibling of SeriesMetadata rather than a second producer of it, for one hard reason
// and one soft one. The hard one: SeriesMetadata.ExternalId is an int, which is AniList's
// id shape, and an eleven-digit MangaUpdates id does not fit in it. The soft one: what
// the two sources publish genuinely differs — MangaUpdates has a publisher and no volume
// entities, AniList has volume entities and no publisher — and one record pretending
// otherwise would carry a column that is always null for whichever source is in hand.
public sealed record MangaUpdatesSeriesMetadata
{
    public required long SourceId { get; init; }

    /// <summary>
    /// The record's own title, marker stripped.
    /// </summary>
    // Named TitleEnglish because that is the column it lands in, and it is honest rather
    // than accurate: MangaUpdates files a large share of series under their romaji title
    // — "Fremd Torturchen", not "Torture Princess: Fremd Torturchen" — and the English
    // name, where it exists at all, is one entry among the Arabic, Russian and Thai ones
    // in `associated`. Guessing which of those is English is a matching problem, and a
    // wrong guess is worse than a romaji title the reader recognises anyway.
    public required string TitleEnglish { get; init; }

    public required Format Format { get; init; }
    public required PublicationStatus PublicationStatus { get; init; }

    public int? StartYear { get; init; }

    /// <summary>The synopsis, already plain text (see <c>HtmlDescriptionSanitizer</c>).</summary>
    public string? Description { get; init; }

    /// <summary>MangaUpdates' CDN URL. Server-side only, like every other source's.</summary>
    public string? CoverImageUrl { get; init; }

    public IReadOnlyList<string> Genres { get; init; } = [];
    public string? Author { get; init; }
    public string? Illustrator { get; init; }

    /// <summary>
    /// The English-language publisher where the record names one, otherwise whichever
    /// it does name. AniList has no publisher field at all, so this is the only source
    /// the column ever gets filled from.
    /// </summary>
    public string? Publisher { get; init; }

    /// <summary>
    /// Volumes the record's free-text status says exist. Null when it says nothing a
    /// parser should trust, and then a series is added tracking none.
    /// </summary>
    public int? VolumeCount { get; init; }

    /// <summary>The status text the count came from, kept verbatim for the log line.</summary>
    public string? RawStatus { get; init; }
}
