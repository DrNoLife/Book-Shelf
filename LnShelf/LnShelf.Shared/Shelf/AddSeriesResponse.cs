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
}
