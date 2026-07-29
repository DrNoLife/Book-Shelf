using LnShelf.Shared.Shelf;

namespace LnShelf.Shared.Metadata;

/// <summary>
/// One search hit from an external metadata source, already in the shelf's own
/// terms. The light novel and the manga of a title are two separate hits — the
/// format is what tells them apart (feature-spec §4.2).
/// </summary>
public sealed record SeriesSearchResultResponse
{
    public required int AniListId { get; init; }

    /// <summary>
    /// Never blank: AniList leaves the English title null for a large share of
    /// entries, so romaji then native stand in.
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
