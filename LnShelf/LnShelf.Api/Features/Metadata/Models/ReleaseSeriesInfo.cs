namespace LnShelf.Api.Features.Metadata.Models;

/// <summary>One search hit from a release source, before any detail is fetched.</summary>
public sealed record ReleaseSeriesCandidate
{
    /// <summary>
    /// The source's own id. A long, not an int: MangaUpdates ids are eleven
    /// digits (45348350266) and do not fit in an Int32.
    /// </summary>
    public required long ExternalId { get; init; }

    public required string Title { get; init; }

    /// <summary>The source's own medium word — "Novel", "Manga", "Doujinshi".</summary>
    public string? SourceType { get; init; }

    public int? StartYear { get; init; }
}

/// <summary>
/// Everything a release source knows about one series.
/// </summary>
public sealed record ReleaseSeriesInfo
{
    public required long ExternalId { get; init; }
    public required string Title { get; init; }

    /// <summary>
    /// Alternative titles, which is what makes linking work at all: MangaUpdates
    /// files "86―EIGHTY-SIX" under the primary title "86" and the recognisable
    /// name only appears here.
    /// </summary>
    public IReadOnlyList<string> AssociatedTitles { get; init; } = [];

    public string? SourceType { get; init; }
    public int? StartYear { get; init; }

    /// <summary>Whether publication has finished, as the source sees it.</summary>
    public bool IsCompleted { get; init; }

    /// <summary>
    /// Volumes the source says exist, parsed out of its free-text status. Null
    /// when the text says nothing a parser should trust.
    /// </summary>
    public int? VolumeCount { get; init; }

    /// <summary>
    /// The status text the count came from, kept verbatim so a discrepancy row can
    /// show what the source actually said.
    /// </summary>
    public string? RawStatus { get; init; }
}
