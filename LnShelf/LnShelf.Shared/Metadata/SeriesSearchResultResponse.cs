using LnShelf.Shared.Shelf;

namespace LnShelf.Shared.Metadata;

/// <summary>
/// One search hit from an external metadata source, already in the shelf's own
/// terms. The light novel and the manga of a title are two separate hits — the
/// format is what tells them apart (feature-spec §4.2).
/// </summary>
public sealed record SeriesSearchResultResponse
{
    /// <summary>Which source this hit came from, and therefore which id is set.</summary>
    // On the row rather than only on the response, because a row is what gets handed
    // back to POST /api/series: the client must be able to say which source's id it is
    // holding without inferring it from which field happens to be null.
    public required MetadataSourceName Source { get; init; }

    /// <summary>AniList's media id. Null when another source answered the search.</summary>
    public int? AniListId { get; init; }

    /// <summary>
    /// MangaUpdates' series id. Null when another source answered the search.
    /// </summary>
    // A long, not an int: MangaUpdates ids are eleven digits (17992364412) and
    // overflow an Int32.
    public long? MangaUpdatesId { get; init; }

    /// <summary>
    /// Never blank: AniList leaves the English title null for a large share of
    /// entries, so romaji then native stand in. MangaUpdates files many series under
    /// their romaji title, so a hit from it is often romaji outright.
    /// </summary>
    public required string TitleEnglish { get; init; }

    public string? TitleRomaji { get; init; }
    public string? TitleNative { get; init; }

    public required Format Format { get; init; }
    public required PublicationStatus PublicationStatus { get; init; }

    /// <summary>
    /// Null when the source does not know the total yet — normal for an ongoing
    /// series, and the reason adding one tracks zero volumes until MangaUpdates
    /// fills them in.
    /// </summary>
    public int? VolumeCount { get; init; }

    public int? StartYear { get; init; }

    /// <summary>
    /// Locally cached cover, or null when caching it failed. The browser never sees
    /// an external CDN URL (design.md).
    /// </summary>
    public string? CoverObjectKey { get; init; }
}
