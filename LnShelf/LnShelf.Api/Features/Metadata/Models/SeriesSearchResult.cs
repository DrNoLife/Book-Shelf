using System.Text.Json.Serialization;

namespace LnShelf.Api.Features.Metadata.Models;

/// <summary>
/// One search hit, mapped out of a source's payload. A clean record: the dirty
/// AniList and MangaUpdates classes stop at the mapper (CLAUDE.md — Types).
/// </summary>
public sealed record SeriesSearchResult
{
    /// <summary>Which source produced this hit.</summary>
    public required MetadataSourceName Source { get; init; }

    /// <summary>The source's own id for the series.</summary>
    // A long rather than AniList's int, because the second source's ids do not fit in
    // one: MangaUpdates numbers series with eleven digits (17992364412). Widening the
    // one field beats carrying two, and <see cref="AniListId"/> is how the AniList-only
    // paths get their int back without a cast at every call site.
    public required long SourceId { get; init; }

    /// <summary>
    /// The AniList media id, for the paths that only ever search AniList — import
    /// matching and the add-series flow.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The hit came from another source, which has no AniList id to give.
    /// </exception>
    // A guarded cast rather than a nullable second column: the caller either knows it is
    // on the AniList path — in which case a nullable would only add a `!` — or it does
    // not, in which case this throwing is the bug report.
    //
    // JsonIgnore is load-bearing, not tidiness. HybridCache stores search results as JSON,
    // and System.Text.Json serializes read-only properties: without this, caching a
    // MangaUpdates result called this getter, the guard fired, and the whole search failed
    // inside the cache layer. It is derived from SourceId anyway, so there is nothing to
    // persist.
    [JsonIgnore]
    public int AniListId => Source is MetadataSourceName.AniList
        ? (int)SourceId
        : throw new InvalidOperationException(
            $"This search hit came from {Source}, which has no AniList id.");

    /// <summary>Never blank; romaji then native stand in when English is absent.</summary>
    public required string TitleEnglish { get; init; }

    public string? TitleRomaji { get; init; }
    public string? TitleNative { get; init; }

    public required Format Format { get; init; }
    public required PublicationStatus PublicationStatus { get; init; }

    /// <summary>Null when the source does not publish a total yet.</summary>
    public int? VolumeCount { get; init; }

    public int? StartYear { get; init; }

    /// <summary>
    /// Who wrote it, when the source credits anyone. Null otherwise.
    /// </summary>
    public string? Author { get; init; }

    /// <summary>
    /// Who drew it, when the source credits anyone. Null otherwise.
    /// </summary>
    // Carried on a *search* hit, not just on the full series, for one reason: it is
    // the only field that separates a light novel from its own manga adaptation.
    // They share every title AniList publishes and the adaptation credits the
    // novelist as Story, so the artist is what breaks the tie — and the import's
    // review queue exists almost entirely because of that tie.
    public string? Illustrator { get; init; }

    /// <summary>
    /// The source's CDN URL. Server-side only — it exists so the cover cache can
    /// fetch the image once; the browser never sees it (design.md).
    /// </summary>
    public string? CoverImageUrl { get; init; }
}
