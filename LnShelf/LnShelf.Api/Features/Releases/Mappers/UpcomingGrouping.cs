namespace LnShelf.Api.Features.Releases.Mappers;

/// <summary>
/// Sorts a shelf's volumes into the Upcoming view's groups.
/// </summary>
// In memory rather than an Expression: the groups are a partition of volumes by a
// comparison against calendar months, and the per-volume "is this out" rule already has
// its one home in SeriesDetailProjection.ToVolume. Calling it here means the Upcoming
// view, the volume tile and the library card are reading the same three grounds.
public static class UpcomingGrouping
{
    /// <summary>How many calendar months after the current one "Next 3 months" spans.</summary>
    private const int NextMonthsSpan = 3;

    /// <param name="series">Loaded with volumes and both user-state rows.</param>
    /// <param name="today">Supplied by the caller so tests can pin it.</param>
    public static UpcomingResponse Group(IEnumerable<Series> series, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(series);

        var endOfThisMonth = EndOfMonth(today);
        var endOfNextMonths = EndOfMonth(today.AddMonths(NextMonthsSpan));

        List<UpcomingEntryResponse> outNow = [];
        List<UpcomingEntryResponse> dated = [];
        List<UpcomingEntryResponse> announced = [];

        foreach (var entry in series)
        {
            var volumes = entry.Volumes
                .OrderBy(volume => volume.Number)
                .Select(volume => SeriesDetailProjection.ToVolume(volume, today, entry.CoverObjectKey))
                .ToList();

            var toBuy = volumes.Where(volume => volume.IsOut && !volume.Owned).ToList();
            if (toBuy.Count > 0)
            {
                // The line's art is the first volume it asks you to buy: that is the
                // spine you are looking for on the shop shelf.
                outNow.Add(ToEntry(entry, toBuy[0].CoverObjectKey, [.. toBuy.Select(volume => volume.Number)]));
            }

            foreach (var volume in volumes.Where(volume => !volume.IsOut && volume.ReleaseDate is not null))
            {
                dated.Add(ToEntry(entry, volume.CoverObjectKey, [volume.Number]) with
                {
                    VolumeTitle = volume.Title,
                    ReleaseDate = volume.ReleaseDate,
                });
            }

            var undated = volumes.Where(volume => !volume.IsOut && volume.ReleaseDate is null).ToList();
            if (undated.Count > 0)
            {
                announced.Add(ToEntry(entry, undated[0].CoverObjectKey, [.. undated.Select(volume => volume.Number)]));
            }
        }

        var byDate = dated
            .OrderBy(entry => entry.ReleaseDate)
            .ThenBy(entry => entry.SeriesTitle, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.VolumeNumbers[0])
            .ToList();

        return new UpcomingResponse
        {
            // Most to buy first — the same "most behind" order the library grid uses.
            OutNow = [.. outNow
                .OrderByDescending(entry => entry.VolumeNumbers.Count)
                .ThenBy(entry => entry.SeriesTitle, StringComparer.OrdinalIgnoreCase)],
            ThisMonth = [.. byDate.Where(entry => entry.ReleaseDate <= endOfThisMonth)],
            NextThreeMonths = [.. byDate.Where(entry => entry.ReleaseDate > endOfThisMonth && entry.ReleaseDate <= endOfNextMonths)],
            Later = [.. byDate.Where(entry => entry.ReleaseDate > endOfNextMonths)],
            Announced = [.. announced.OrderBy(entry => entry.SeriesTitle, StringComparer.OrdinalIgnoreCase)],
        };
    }

    private static UpcomingEntryResponse ToEntry(Series series, string? coverObjectKey, IReadOnlyList<int> volumeNumbers) => new()
    {
        SeriesId = series.Id,
        SeriesTitle = series.TitleEnglish,
        Format = series.Format,
        CoverObjectKey = coverObjectKey,
        VolumeNumbers = volumeNumbers,
    };

    private static DateOnly EndOfMonth(DateOnly date) =>
        new(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month));
}
