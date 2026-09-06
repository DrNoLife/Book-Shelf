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
    public async Task EnrichAsync(
        Series series,
        SyncRun run,
        DateOnly today,
        LookupBudget budget,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(budget);

        await EnrichFromCatalogueAsync(series, run, today, budget, cancellationToken);
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
        CancellationToken cancellationToken)
    {
        if (budget.IsExhausted || !series.Volumes.Any(VolumeGaps.NeedsAnything))
        {
            return;
        }

        var matched = await catalogueTitleArbiter.AskAsync(series, run, budget, cancellationToken);

        foreach (var volume in series.Volumes.OrderBy(volume => volume.Number))
        {
            if (!matched.TryGetValue(volume.Number, out var record))
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
