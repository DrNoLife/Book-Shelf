using LnShelf.Shared.Preferences;

namespace LnShelf.Shared.Shelf;

/// <summary>
/// Everything the series-detail page renders, in one round trip.
/// </summary>
public sealed record SeriesDetailResponse
{
    public required Guid SeriesId { get; init; }

    public required string TitleEnglish { get; init; }
    public string? TitleRomaji { get; init; }
    public string? TitleNative { get; init; }

    public required Format Format { get; init; }
    public required PublicationStatus PublicationStatus { get; init; }
    public required TrackingStatus TrackingStatus { get; init; }

    public string? Author { get; init; }
    public string? Illustrator { get; init; }

    /// <summary>Null for AniList-sourced series — it has no publisher field.</summary>
    public string? Publisher { get; init; }

    public int? StartYear { get; init; }
    public required IReadOnlyList<string> Genres { get; init; }

    public string? CoverObjectKey { get; init; }

    /// <summary>External ids, for the links out. Null hides its link.</summary>
    public int? AniListId { get; init; }

    public long? MangaUpdatesId { get; init; }

    public string? Notes { get; init; }
    public DateOnly? StartedAt { get; init; }
    public DateOnly? LastReadAt { get; init; }
    public required bool HasNewVolume { get; init; }

    /// <summary>
    /// This series' own volume view, or null to follow the global preference.
    /// </summary>
    // A per-series override rather than one global switch, because the right view is a
    // property of the series: 42 manga volumes want a list, 14 light novels with art
    // want tiles.
    public VolumeView? VolumeViewOverride { get; init; }

    public required SeriesCountsResponse Counts { get; init; }

    /// <summary>
    /// Null when no volume has a purchase price.
    /// </summary>
    // Null rather than zeroes: "0 kr · avg 0 kr" is a lie about a shelf whose prices
    // were simply never recorded, and an imported library has none.
    public SeriesSpendResponse? Spend { get; init; }

    /// <summary>Null until a relation is synced (phase 2's AniList relations).</summary>
    public SeriesRelationResponse? Relation { get; init; }

    public required IReadOnlyList<VolumeResponse> Volumes { get; init; }
}

/// <summary>The hero's numbers, on the same rules as the library card.</summary>
public sealed record SeriesCountsResponse
{
    public required int Read { get; init; }
    public required int Owned { get; init; }
    public required int Published { get; init; }
    public required int ToBuy { get; init; }
    public required int Backlog { get; init; }
    public required int Tracked { get; init; }

    /// <summary>Feeds the "Vol 12 on Saxo" call to action. Null when caught up.</summary>
    public int? NextToBuyNumber { get; init; }

    /// <summary>Volumes the shelf knows of that are not out yet.</summary>
    public required int NotOut { get; init; }

    /// <summary>Of those, how many carry a date.</summary>
    public required int Dated { get; init; }
}

/// <summary>
/// The spend line — feature-spec §4.7's one v1 exception to Stats being post-v1.
/// </summary>
public sealed record SeriesSpendResponse
{
    /// <summary>Volumes that actually carry a price, not volumes owned.</summary>
    public required int PricedCount { get; init; }

    public required decimal Total { get; init; }
    public required decimal Average { get; init; }
}

/// <summary>The LN ↔ manga link, when one has been synced.</summary>
public sealed record SeriesRelationResponse
{
    public required Guid SeriesId { get; init; }
    public required string TitleEnglish { get; init; }
    public required Format Format { get; init; }
    public required int Published { get; init; }
    public required int Owned { get; init; }
}
