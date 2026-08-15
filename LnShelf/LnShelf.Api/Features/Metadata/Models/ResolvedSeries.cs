namespace LnShelf.Api.Features.Metadata.Models;

/// <summary>
/// One search hit resolved to full detail, in terms neither source's own record uses.
/// </summary>
// Exists for `enrichment-verify`, which has to check the same three things — a title, a
// synopsis, a cover that decodes — about a series it may have found through either
// source. Without it that command would need two copies of every check, one per source's
// record shape, and the two would drift.
public sealed record ResolvedSeries
{
    public required MetadataSourceName Source { get; init; }
    public required long SourceId { get; init; }

    public required string TitleEnglish { get; init; }
    public string? TitleRomaji { get; init; }
    public string? TitleNative { get; init; }

    public required Format Format { get; init; }
    public required PublicationStatus PublicationStatus { get; init; }

    public string? Description { get; init; }
    public string? CoverImageUrl { get; init; }

    /// <summary>
    /// Volumes the resolving source itself listed. AniList publishes a count and this is
    /// it; MangaUpdates lists no volume entities at all, so it is zero there.
    /// </summary>
    public required int ListedVolumeCount { get; init; }

    /// <summary>
    /// The release source's volume count, set only when the source that answered *is* the
    /// release source.
    /// </summary>
    // Its own field rather than folded into ListedVolumeCount, because the conflict rule
    // gives volume counts to MangaUpdates and the report says which authority a run used.
    // When this is set, no title matching is needed to establish the count — the id
    // already came from the release source.
    public int? ReleaseVolumeCount { get; init; }
}
