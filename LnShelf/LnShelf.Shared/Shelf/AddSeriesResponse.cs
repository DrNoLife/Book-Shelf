namespace LnShelf.Shared.Shelf;

/// <summary>
/// The result of adding a series to the shelf.
/// </summary>
public sealed record AddSeriesResponse
{
    public required Guid SeriesId { get; init; }
    public required string TitleEnglish { get; init; }

    /// <summary>The format that was stored, which is the metadata source's.</summary>
    public required Format Format { get; init; }

    /// <summary>Volumes now tracked for the series.</summary>
    public required int VolumeCount { get; init; }

    /// <summary>
    /// True when the series was already on the shelf and nothing was written.
    /// Adding twice is a no-op, not a duplicate.
    /// </summary>
    public required bool AlreadyTracked { get; init; }

    /// <summary>
    /// What the refresh that ran alongside the add managed to fetch, in the same words
    /// the sidebar prints. Null when nothing was refreshed.
    /// </summary>
    // Reported rather than left implicit: adding a series now links it to the release
    // source and enriches whatever volumes carry an ISBN, and how much of that succeeded
    // is exactly what the person who pressed Add wants to know. AniList alone would leave
    // a series with volumes and nothing else true about them.
    public string? RefreshSummary { get; init; }
}
