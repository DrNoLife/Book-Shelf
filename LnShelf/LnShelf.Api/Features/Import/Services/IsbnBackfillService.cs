namespace LnShelf.Api.Features.Import.Services;

/// <inheritdoc />
// Every import since the first one parsed the export's ISBN column into
// `import_row.Isbn` — and then nothing read it, because `Volume` had no column to put it
// in. Seventy-three identifiers were left stranded in staging, covering 59% of the owned
// shelf, at zero external cost
// (.research/volume-enrichment/e-isbn-first-pipeline.md).
//
// Two callers now: the one-shot command that collected them for the rows already
// committed, and the add-series flow, which adopts them for one series so enrichment has
// something to look up. Both are idempotent — an ISBN already on a volume is never
// replaced — which is what makes either safe to run whenever.
public sealed class IsbnBackfillService(LnShelfDbContext dbContext, ILogger<IsbnBackfillService> logger)
    : IIsbnBackfill
{
    public Task<IsbnBackfillReport> RunAsync(CancellationToken cancellationToken) =>
        ApplyAsync(seriesId: null, cancellationToken);

    public Task<IsbnBackfillReport> RunForSeriesAsync(Guid seriesId, CancellationToken cancellationToken) =>
        ApplyAsync(seriesId, cancellationToken);

    private async Task<IsbnBackfillReport> ApplyAsync(Guid? seriesId, CancellationToken cancellationToken)
    {
        // Tracked, not AsNoTracking: this query is the write.
        var shelfQuery = dbContext.Series
            .Include(series => series.Volumes)
            .Where(series => series.AniListId != null);

        if (seriesId is { } id)
        {
            shelfQuery = shelfQuery.Where(series => series.Id == id);
        }

        var shelf = await shelfQuery.ToDictionaryAsync(series => series.AniListId!.Value, cancellationToken);

        if (shelf.Count is 0)
        {
            // Nothing to land on. A manual series with no AniList id has no staging rows
            // that could refer to it, so there is nothing to look for either.
            return Empty;
        }

        // Only the groups that could name a volume in scope. For one series that is one
        // AniList id, which keeps the add path from reading the whole staging table.
        var aniListIds = shelf.Keys.ToList();

        var groups = await dbContext.ImportGroups
            .Include(group => group.Rows)
            .Where(group => group.MatchedAniListId != null
                            && aniListIds.Contains(group.MatchedAniListId.Value))
            .ToListAsync(cancellationToken);

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

        // Quiet when there was nothing to do, which is the add path's usual answer: a
        // series the shelf has never imported has no staged ISBNs, and saying so on every
        // add would be noise.
        if (offered > 0)
        {
            logger.LogInformation(
                "ISBN backfill over {GroupCount} committed import group(s): {Offered} staged, {Written} written, "
                + "{AlreadyRecorded} already recorded, {WithoutVolume} with no volume to land on",
                groups.Count,
                report.Offered,
                report.Written,
                report.AlreadyRecorded,
                report.WithoutVolume);
        }

        return report;
    }

    private static IsbnBackfillReport Empty { get; } = new()
    {
        Offered = 0,
        Written = 0,
        AlreadyRecorded = 0,
        WithoutVolume = 0,
    };
}
