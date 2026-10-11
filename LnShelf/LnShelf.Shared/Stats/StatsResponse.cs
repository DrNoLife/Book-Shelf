using LnShelf.Shared.Shelf;

namespace LnShelf.Shared.Stats;

/// <summary>
/// The Stats page: what has been read, bought and finished (ui-spec §5).
/// </summary>
// Everything here is computed from the shelf as it stands. There is deliberately no
// backlog-over-time chart: nothing records when a volume was bought for most of the
// shelf (an import carries no purchase dates), so a trend line would be drawn from the
// few volumes that happen to have one and would misstate the rest.
public sealed record StatsResponse
{
    /// <summary>One row per format that has at least one series, in enum order.</summary>
    public required IReadOnlyList<FormatStatsResponse> Formats { get; init; }

    /// <summary>The last twelve calendar months, oldest first, current month last.</summary>
    public required IReadOnlyList<MonthlyReadResponse> ReadPerMonth { get; init; }

    /// <summary>
    /// Read volumes with no read date. They count in the totals and cannot be placed
    /// on the monthly chart, which is why the page says how many there are.
    /// </summary>
    public required int ReadWithoutDate { get; init; }

    /// <summary>The same twelve months, from volumes that carry a purchase date and price.</summary>
    public required IReadOnlyList<MonthlySpendResponse> SpendPerMonth { get; init; }

    /// <summary>All-time spend over every priced volume; null when nothing carries a price.</summary>
    public SeriesSpendResponse? Spend { get; init; }

    /// <summary>Series the reader has marked Completed.</summary>
    public required int CompletedSeries { get; init; }

    /// <summary>Series the reader has started: everything except Plan to start.</summary>
    public required int StartedSeries { get; init; }

    /// <summary>The five series with the most volumes read, most first.</summary>
    public required IReadOnlyList<LongestSeriesResponse> LongestSeries { get; init; }

    /// <summary>The earliest read date on the shelf; null when no read volume carries one.</summary>
    public DateOnly? FirstReadAt { get; init; }

    /// <summary>The last few volumes marked read with a date, latest first (the profile page).</summary>
    public IReadOnlyList<RecentlyReadResponse> RecentlyRead { get; init; } = [];
}

/// <summary>One volume the reader finished, for the profile's "Recently finished".</summary>
public sealed record RecentlyReadResponse
{
    public required Guid SeriesId { get; init; }
    public required string SeriesTitle { get; init; }
    public required int VolumeNumber { get; init; }
    public required DateOnly ReadAt { get; init; }
}

/// <summary>One format's share of the shelf.</summary>
public sealed record FormatStatsResponse
{
    public required Format Format { get; init; }
    public required int SeriesCount { get; init; }
    public required int Read { get; init; }
    public required int Owned { get; init; }

    /// <summary>Owned and not read.</summary>
    public required int Backlog { get; init; }

    /// <summary>Released and not owned.</summary>
    public required int ToBuy { get; init; }
}

/// <summary>Volumes read in one calendar month, split by format.</summary>
public sealed record MonthlyReadResponse
{
    /// <summary>The first day of the month.</summary>
    public required DateOnly Month { get; init; }

    public required int LightNovel { get; init; }
    public required int Manga { get; init; }
    public required int WebNovel { get; init; }

    public int Total => LightNovel + Manga + WebNovel;
}

/// <summary>Money spent in one calendar month.</summary>
public sealed record MonthlySpendResponse
{
    /// <summary>The first day of the month.</summary>
    public required DateOnly Month { get; init; }

    public required decimal Total { get; init; }
    public required int VolumeCount { get; init; }
}

/// <summary>One of the series read furthest into.</summary>
public sealed record LongestSeriesResponse
{
    public required Guid SeriesId { get; init; }
    public required string Title { get; init; }
    public required Format Format { get; init; }
    public required int Read { get; init; }
    public required int Tracked { get; init; }

    /// <summary>Owned volumes, read or not — the bar's middle segment.</summary>
    public required int Owned { get; init; }

    /// <summary>Volumes out now, by the same rule the volume tile uses — the bar's span.</summary>
    public required int Out { get; init; }

    /// <summary>Key for <c>/api/covers/{objectKey}</c>. Null renders the placeholder.</summary>
    public string? CoverObjectKey { get; init; }
}
