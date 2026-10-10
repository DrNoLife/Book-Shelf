namespace LnShelf.Shared.Sharing;

/// <summary>
/// What a visitor with the link sees (feature-spec §4.8). Fixed and small by design.
/// </summary>
// No name, email or id of the owner anywhere in here: the page is public, and the only
// thing a visitor is meant to learn is what is on the shelf and how far it has been read.
// SharedShelfContractTests lists the allowed fields, so adding one is a decision.
public sealed record SharedShelfResponse
{
    public required SharedShelfTotals Totals { get; init; }

    /// <summary>Reading first, then completed, then plan to start. Dropped series are never here.</summary>
    public required IReadOnlyList<SharedSeriesCardResponse> Series { get; init; }
}

/// <summary>The three totals over a shared shelf.</summary>
public sealed record SharedShelfTotals
{
    public required int VolumesRead { get; init; }
    public required int SeriesReading { get; init; }
    public required int SeriesCompleted { get; init; }
}
