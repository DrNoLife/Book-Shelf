namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// Fills in a series' volumes from the two per-volume sources: the store catalogue first,
/// then a per-ISBN lookup for whatever is still missing.
/// </summary>
// Extracted from SeriesSyncService rather than added to it. That class's job is "walk the
// shelf, reconcile each series against the release source"; this one's is "which volumes
// are worth asking about, and who to ask". What may be written from an answer lives one
// step further out again, in VolumeFactWriter, because both passes here share those rules.
//
// The order of the two passes is the precedence order, and it is deliberate. The catalogue
// answers for a whole series in one request and carries the fields the ISBN lookup does
// not (a blurb, and a date for volumes with no ISBN at all); the ISBN lookup is
// unambiguous but only reaches the 59% of volumes whose ISBN is known. Catalogue first,
// gaps second.
//
// The two passes themselves live in CatalogueTitleArbiter and IsbnVolumeEnricher. This is
// the pipeline: run them in that order, and write what the catalogue pass matched.
public sealed class VolumeEnrichmentService(
    IVolumeCatalogueSource catalogueSource,
    CatalogueTitleArbiter catalogueTitleArbiter,
    IsbnVolumeEnricher isbnEnricher,
    BonusVolumeWriter bonusVolumeWriter,
    VolumeFactWriter factWriter)
{
    /// <param name="today">
    /// "In the future" is relative to a day, so the caller supplies it — the same
    /// convention the projections follow.
    /// </param>
    /// <param name="budget">
    /// Spent down as requests are made, not as volumes are visited: the catalogue costs one
    /// for the whole series, and one volume can cost several when its own ISBN turns out
    /// not to be indexed.
    /// </param>
    /// <param name="expectedBonusVolumes">
    /// How many bonus volumes the release source counts beside the main ones. More than the
    /// shelf tracks is a reason to ask the store even when every volume is already enriched.
    /// </param>
    public async Task EnrichAsync(
        Series series,
        SyncRun run,
        DateOnly today,
        LookupBudget budget,
        int expectedBonusVolumes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(budget);

        await EnrichFromCatalogueAsync(series, run, today, budget, expectedBonusVolumes, cancellationToken);
        await isbnEnricher.EnrichAsync(series, run, today, budget, cancellationToken);
    }

    /// <summary>
    /// Ask the store, under whichever of this series' names it answers to, and write what
    /// came back onto the volumes it named.
    /// </summary>
    private async Task EnrichFromCatalogueAsync(
        Series series,
        SyncRun run,
        DateOnly today,
        LookupBudget budget,
        int expectedBonusVolumes,
        CancellationToken cancellationToken)
    {
        var missesBonusVolumes =
            series.Volumes.Count(volume => volume.Number.IsBonusVolume) < expectedBonusVolumes;

        if (budget.IsExhausted || (!series.Volumes.Any(VolumeGaps.NeedsAnything) && !missesBonusVolumes))
        {
            return;
        }

        var match = await catalogueTitleArbiter.AskAsync(series, run, budget, cancellationToken);

        // Only when the release source says the edition has bonus volumes at all: the store is
        // trusted with where one sits, not with whether there is one. Created before the facts
        // are written, so a new bonus volume gets its cover, date and subtitle in this pass.
        if (expectedBonusVolumes > 0)
        {
            bonusVolumeWriter.Create(series, match, run);
        }

        foreach (var volume in series.Volumes.OrderBy(volume => volume.Number))
        {
            if (!match.ByVolumeNumber.TryGetValue(volume.Number, out var record)
                && !match.BonusVolumes.TryGetValue(volume.Number, out record))
            {
                continue;
            }

            await factWriter.ApplyAsync(
                series,
                volume,
                VolumeFactsMapper.ToFacts(record, catalogueSource.SourceName),
                run,
                today,
                cancellationToken);
        }
    }
}
