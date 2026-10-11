namespace LnShelf.Api.Features.Stats.Mappers;

/// <summary>
/// Turns the loaded shelf into the Stats page.
/// </summary>
// In memory, like SeriesDetailProjection: every figure here is a sum or a group-by over
// volumes, and "out" / "owned" are read through SeriesDetailProjection.ToVolume so the
// page's To buy and Backlog are the library's, not a second copy of the rule.
public static class StatsCalculator
{
    private const int MonthsShown = 12;
    private const int LongestSeriesShown = 5;
    private const int RecentlyReadShown = 5;

    /// <param name="series">Loaded with volumes and both user-state rows.</param>
    /// <param name="today">Supplied by the caller so tests can pin it.</param>
    public static StatsResponse Calculate(IReadOnlyCollection<Series> series, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(series);

        var shelf = series
            .Select(entry => new ShelvedSeries(
                entry,
                [.. entry.Volumes.Select(volume => SeriesDetailProjection.ToVolume(volume, today))]))
            .ToList();

        var months = LastMonths(today);
        var readVolumes = shelf
            .SelectMany(entry => entry.Volumes
                .Where(volume => volume.ReadStatus is ReadStatus.Read)
                .Select(volume => new ReadVolume(entry.Series.Format, volume.ReadAt)))
            .ToList();

        return new StatsResponse
        {
            Formats = ToFormats(shelf),
            ReadPerMonth = [.. months.Select(month => ToMonthlyRead(month, readVolumes))],
            ReadWithoutDate = readVolumes.Count(volume => volume.ReadAt is null),
            SpendPerMonth = ToMonthlySpend(months, series),
            Spend = ToSpend(shelf),
            CompletedSeries = shelf.Count(entry => TrackingOf(entry.Series) is TrackingStatus.Completed),
            StartedSeries = shelf.Count(entry => TrackingOf(entry.Series) is not TrackingStatus.PlanToStart),
            LongestSeries = ToLongestSeries(shelf),
            FirstReadAt = ToDatedReads(shelf).Select(read => (DateOnly?)read.ReadAt).Min(),
            RecentlyRead = ToRecentlyRead(shelf),
        };
    }

    private static List<FormatStatsResponse> ToFormats(List<ShelvedSeries> shelf) =>
    [
        .. shelf
            .GroupBy(entry => entry.Series.Format)
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                var volumes = group.SelectMany(entry => entry.Volumes).ToList();

                return new FormatStatsResponse
                {
                    Format = group.Key,
                    SeriesCount = group.Count(),
                    Read = volumes.Count(volume => volume.ReadStatus is ReadStatus.Read),
                    Owned = volumes.Count(volume => volume.Owned),
                    Backlog = volumes.Count(volume => volume.Owned && volume.ReadStatus is not ReadStatus.Read),
                    ToBuy = volumes.Count(volume => volume.IsOut && !volume.Owned),
                };
            }),
    ];

    private static MonthlyReadResponse ToMonthlyRead(DateOnly month, List<ReadVolume> readVolumes)
    {
        var inMonth = readVolumes.Where(volume => IsInMonth(volume.ReadAt, month)).ToList();

        return new MonthlyReadResponse
        {
            Month = month,
            LightNovel = inMonth.Count(volume => volume.Format is Format.LightNovel),
            Manga = inMonth.Count(volume => volume.Format is Format.Manga),
            WebNovel = inMonth.Count(volume => volume.Format is Format.WebNovel),
        };
    }

    // Read off the user-state rows rather than VolumeResponse, which carries the price
    // but not the purchase date.
    private static List<MonthlySpendResponse> ToMonthlySpend(List<DateOnly> months, IEnumerable<Series> series)
    {
        var purchases = series
            .SelectMany(entry => entry.Volumes)
            .Select(volume => volume.UserState)
            .OfType<VolumeUserState>()
            .Where(state => state is { Owned: true, PurchasePrice: > 0, PurchasedAt: not null })
            .ToList();

        return
        [
            .. months.Select(month =>
            {
                var inMonth = purchases.Where(state => IsInMonth(state.PurchasedAt, month)).ToList();

                return new MonthlySpendResponse
                {
                    Month = month,
                    Total = inMonth.Sum(state => state.PurchasePrice!.Value),
                    VolumeCount = inMonth.Count,
                };
            }),
        ];
    }

    // The same rule as the series hero's spend line: averaged over what carries a price,
    // not over everything owned, so a partial record does not understate the average.
    private static SeriesSpendResponse? ToSpend(List<ShelvedSeries> shelf)
    {
        var prices = shelf
            .SelectMany(entry => entry.Volumes)
            .Where(volume => volume.Owned && volume.PurchasePrice is > 0)
            .Select(volume => volume.PurchasePrice!.Value)
            .ToList();

        if (prices.Count == 0)
        {
            return null;
        }

        var total = prices.Sum();

        return new SeriesSpendResponse
        {
            PricedCount = prices.Count,
            Total = total,
            Average = decimal.Round(total / prices.Count, 0, MidpointRounding.AwayFromZero),
        };
    }

    private static List<LongestSeriesResponse> ToLongestSeries(List<ShelvedSeries> shelf) =>
    [
        .. shelf
            .Select(entry => new LongestSeriesResponse
            {
                SeriesId = entry.Series.Id,
                Title = entry.Series.TitleEnglish,
                Format = entry.Series.Format,
                Read = entry.Volumes.Count(volume => volume.ReadStatus is ReadStatus.Read),
                Tracked = entry.Volumes.Count,
                Owned = entry.Volumes.Count(volume => volume.Owned),
                Out = entry.Volumes.Count(volume => volume.IsOut),
                CoverObjectKey = entry.Series.CoverObjectKey,
            })
            .Where(longest => longest.Read > 0)
            .OrderByDescending(longest => longest.Read)
            .ThenBy(longest => longest.Title, StringComparer.OrdinalIgnoreCase)
            .Take(LongestSeriesShown),
    ];

    private static List<RecentlyReadResponse> ToRecentlyRead(List<ShelvedSeries> shelf) =>
    [
        .. ToDatedReads(shelf)
            .OrderByDescending(read => read.ReadAt)
            .ThenBy(read => read.SeriesTitle, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(read => read.VolumeNumber)
            .Take(RecentlyReadShown),
    ];

    private static IEnumerable<RecentlyReadResponse> ToDatedReads(List<ShelvedSeries> shelf) =>
        shelf.SelectMany(entry => entry.Volumes
            .Where(volume => volume is { ReadStatus: ReadStatus.Read, ReadAt: not null })
            .Select(volume => new RecentlyReadResponse
            {
                SeriesId = entry.Series.Id,
                SeriesTitle = entry.Series.TitleEnglish,
                VolumeNumber = volume.Number,
                ReadAt = volume.ReadAt!.Value,
            }));

    /// <summary>The first day of each of the last twelve months, oldest first.</summary>
    private static List<DateOnly> LastMonths(DateOnly today)
    {
        var current = new DateOnly(today.Year, today.Month, 1);

        return [.. Enumerable.Range(0, MonthsShown).Select(offset => current.AddMonths(offset - (MonthsShown - 1)))];
    }

    private static bool IsInMonth(DateOnly? date, DateOnly month) =>
        date is { } value && value.Year == month.Year && value.Month == month.Month;

    // A series with no user-state row has not been shelved yet — the library card's rule.
    private static TrackingStatus TrackingOf(Series series) =>
        series.UserState?.TrackingStatus ?? TrackingStatus.PlanToStart;

    private sealed record ShelvedSeries(Series Series, IReadOnlyList<VolumeResponse> Volumes);

    private sealed record ReadVolume(Format Format, DateOnly? ReadAt);
}
