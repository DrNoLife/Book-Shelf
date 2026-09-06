namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// Which of a store catalogue's records are this series' volumes.
/// </summary>
// Two filters, both of them <see cref="VolumeEditionMatcher"/>'s, and both necessary. A
// store search for "Slime light novel" answers with the manga, the German editions, the
// spin-offs and volumes of unrelated series that happen to share a word; TryMatchVolumeNumber
// refuses everything whose title does not name this series with a number leading straight
// after it, and ConfirmsMedium then insists the record say which medium it is rather than
// merely fail to say it is the other one. The enrichment pass writes to the shelf with
// nobody looking, which is the bar that earns the stricter of the two.
//
// One class rather than two loops: the enrichment pass wants the record for each volume it
// tracks, and the verification command wants the highest volume number the store confirms.
// Those were the same twenty lines twice over, one keeping a dictionary and one a maximum,
// so this walks the records once and answers both.
public sealed class CatalogueVolumeMatcher(ILogger<CatalogueVolumeMatcher> logger)
{
    /// <param name="catalogueTitle">
    /// The title this answer was fetched under, not necessarily the shelf's own. It has to
    /// be the one the matcher compares against: when the store answered under an
    /// alternative, its records name *that* title — "Konosuba: God's Blessing on This
    /// Wonderful World!, Vol. 3 (light novel)" for a series the shelf calls "Kono Subarashii
    /// Sekai ni Shukufuku o!" — and the shelf's own title appears nowhere in them. The
    /// subtitle extractor needs no such care; it anchors on the volume number, not the title.
    /// </param>
    // First confirmed record per volume number wins. Apple lists a volume more than once —
    // a bundle, a re-release — and the ranking puts the plain edition first.
    //
    // Volume numbers the shelf does not track are ignored outright for the per-volume
    // answer. The release source owns volume counts (CLAUDE.md), so a store listing volume
    // 25 of a series the count says has 23 is not grounds to create two volumes; it is a
    // store carrying a spin-off or a future listing under a number this shelf has no
    // opinion about. They still count towards the highest confirmed number, which is asked
    // precisely when no source has stated a count at all.
    public CatalogueMatch Match(
        Series series,
        string catalogueTitle,
        IReadOnlyList<CatalogueVolume> catalogue)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(catalogue);

        // Folded once for the whole answer rather than once per record, which is the
        // difference between one decomposition and two hundred of them per title tried.
        var normalizedSeriesTitle = NormalizedTitle.From(catalogueTitle);

        var tracked = series.Volumes.Select(volume => volume.Number).ToHashSet();
        var byVolumeNumber = new Dictionary<int, CatalogueVolume>();
        var highestConfirmed = 0;

        foreach (var record in catalogue)
        {
            var recordTitle = NormalizedTitle.From(record.Title);

            if (!VolumeEditionMatcher.TryMatchVolumeNumber(
                    normalizedSeriesTitle, series.Format, recordTitle, out var number))
            {
                continue;
            }

            var confirmsMedium = VolumeEditionMatcher.ConfirmsMedium(series.Format, recordTitle);

            if (confirmsMedium && number > highestConfirmed)
            {
                highestConfirmed = number;
            }

            if (!tracked.Contains(number) || byVolumeNumber.ContainsKey(number))
            {
                continue;
            }

            if (!confirmsMedium)
            {
                logger.LogInformation(
                    "{TitleEnglish} volume {VolumeNumber}: {RecordKey} (\"{RecordTitle}\") is the right "
                    + "volume but does not state that it is a {Format}; skipped",
                    series.TitleEnglish,
                    number,
                    record.SourceRecordKey,
                    record.Title,
                    series.Format);

                continue;
            }

            byVolumeNumber[number] = record;
        }

        return new CatalogueMatch
        {
            ByVolumeNumber = byVolumeNumber,
            HighestConfirmedNumber = highestConfirmed,
        };
    }
}

/// <summary>What one catalogue answer turned out to say about a series.</summary>
public sealed record CatalogueMatch
{
    /// <summary>The record for each volume number the shelf tracks and the store confirmed.</summary>
    public required IReadOnlyDictionary<int, CatalogueVolume> ByVolumeNumber { get; init; }

    /// <summary>
    /// The highest volume number the store positively confirms for this series, tracked or
    /// not. Zero when it confirms none.
    /// </summary>
    public required int HighestConfirmedNumber { get; init; }
}
