namespace LnShelf.Api.Features.Metadata.Models;

/// <summary>
/// Everything a metadata source knows about one series, in the shelf's terms.
/// This is what the add-series flow persists from.
/// </summary>
public sealed record SeriesMetadata
{
    /// <summary>The source's own id — AniList's media id today.</summary>
    public required int ExternalId { get; init; }

    /// <summary>Never blank; romaji then native stand in when English is absent.</summary>
    public required string TitleEnglish { get; init; }

    public string? TitleRomaji { get; init; }
    public string? TitleNative { get; init; }

    public required Format Format { get; init; }
    public required PublicationStatus PublicationStatus { get; init; }

    public int? StartYear { get; init; }

    /// <summary>
    /// The synopsis, already plain text — the source's markup is removed at the edge
    /// (see <c>AniListDescriptionSanitizer</c>). Null when the source has none.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// The source's CDN URL. Server-side only — the cover cache fetches it once
    /// and the browser only ever sees the local cover route (design.md).
    /// </summary>
    public string? CoverImageUrl { get; init; }

    public IReadOnlyList<string> Genres { get; init; } = [];
    public string? Author { get; init; }
    public string? Illustrator { get; init; }

    /// <summary>
    /// Publisher is not part of AniList's media payload at all; MangaUpdates
    /// carries it, so it stays null until phase 2.
    /// </summary>
    public string? Publisher { get; init; }

    /// <summary>
    /// Empty when the source does not know the volume count — an ongoing series
    /// commonly has none. No volume is ever invented to fill the gap.
    /// </summary>
    public IReadOnlyList<VolumeMetadata> Volumes { get; init; } = [];
}
