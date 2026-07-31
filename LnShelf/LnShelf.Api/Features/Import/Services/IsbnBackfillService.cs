namespace LnShelf.Api.Features.Import.Services;

/// <summary>
/// Carries the ISBNs already sitting in import staging onto the volumes they
/// identify. Idempotent: a second run writes nothing.
/// </summary>
// Every import since the first one parsed the export's ISBN column into
// `import_row.Isbn` — and then nothing read it, because `Volume` had no column to
// put it in. Seventy-three identifiers were left stranded in staging, covering 59%
// of the owned shelf, at zero external cost
// (.research/volume-enrichment/e-isbn-first-pipeline.md).
//
// This is the one-shot that collects them for the rows already committed;
// ImportShelfWriter carries them forward from now on, so this is expected to become
// a no-op and stay one. It is deliberately not part of startup: a backfill that runs
// itself on every boot is a migration pretending to be a service.
public sealed class IsbnBackfillService(LnShelfDbContext dbContext, ILogger<IsbnBackfillService> logger)
{
    public async Task<IsbnBackfillReport> RunAsync(CancellationToken cancellationToken)
    {
        var groups = await dbContext.ImportGroups
            .Include(group => group.Rows)
            .Where(group => group.MatchedAniListId != null)
            .ToListAsync(cancellationToken);

        // Tracked, not AsNoTracking: this query is the write.
        var shelf = await dbContext.Series
            .Include(series => series.Volumes)
            .Where(series => series.AniListId != null)
            .ToDictionaryAsync(series => series.AniListId!.Value, cancellationToken);

        var offered = 0;
        var written = 0;
        var alreadyRecorded = 0;
        var withoutVolume = 0;

        foreach (var seriesGroups in groups.GroupBy(group => group.MatchedAniListId!.Value))
        {
            // The same merge the commit runs, rather than a second join written by
            // hand: the rule for which of two editions' ISBNs wins then exists once.
            var desiredStates = DesiredVolumeStateMerger.Merge(seriesGroups);

            shelf.TryGetValue(seriesGroups.Key, out var series);

            foreach (var desired in desiredStates.Values.Where(state => state.Isbn is not null))
            {
                offered++;

                var volume = series?.Volumes.FirstOrDefault(candidate => candidate.Number == desired.VolumeNumber);

                if (volume is null)
                {
                    withoutVolume++;
                    continue;
                }

                if (volume.Isbn is not null)
                {
                    alreadyRecorded++;
                    continue;
                }

                volume.Isbn = desired.Isbn;
                written++;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var report = new IsbnBackfillReport
        {
            Offered = offered,
            Written = written,
            AlreadyRecorded = alreadyRecorded,
            WithoutVolume = withoutVolume,
        };

        logger.LogInformation(
            "ISBN backfill over {GroupCount} committed import group(s): {Offered} staged, {Written} written, "
            + "{AlreadyRecorded} already recorded, {WithoutVolume} with no volume to land on",
            groups.Count,
            report.Offered,
            report.Written,
            report.AlreadyRecorded,
            report.WithoutVolume);

        return report;
    }
}
