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
// The volumes the store may create have their own writers: BonusVolumeWriter,
// WholeExtraVolumeWriter, and NextMainVolumeWriter for the next announced main volumes.
public sealed class VolumeEnrichmentService(
    IVolumeCatalogueSource catalogueSource,
    CatalogueTitleArbiter catalogueTitleArbiter,
    IsbnVolumeEnricher isbnEnricher,
    BonusVolumeWriter bonusVolumeWriter,
    WholeExtraVolumeWriter wholeExtraVolumeWriter,
    NextMainVolumeWriter nextMainVolumeWriter,
    VolumeFactWriter factWriter)
{
    /// <param name="today">
    /// "In the future" is relative to a day, so the caller supplies it — the same
    /// convention the projections follow.
    /// </param>
    /// <param name="budget">
    /// Spent down as requests are made, not as volumes are visited, and per source: the
    /// catalogue pass draws on <see cref="EnrichmentBudget.Catalogue"/> (one request per title
    /// attempted), the by-ISBN pass on <see cref="EnrichmentBudget.ByIsbn"/> (one volume can
    /// cost several when its own ISBN turns out not to be indexed).
    /// </param>
    /// <param name="expectedBonusVolumes">
    /// How many extras the release source counts beside the main ones: bonus volumes such as
    /// Vol. 7.5, or whole volumes past the main count. More than the shelf tracks is a reason
    /// to ask the store even when every volume is already enriched.
    /// </param>
    /// <returns>
    /// The next main volumes the store created in this pass, for the caller to decide whether
    /// they are news.
    /// </returns>
    public async Task<IReadOnlyList<Volume>> EnrichAsync(
        Series series,
        SyncRun run,
        DateOnly today,
        EnrichmentBudget budget,
        int expectedBonusVolumes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(budget);

        var nextVolumes = await EnrichFromCatalogueAsync(
            series, run, today, budget, expectedBonusVolumes, cancellationToken);
        await isbnEnricher.EnrichAsync(series, run, today, budget.ByIsbn, cancellationToken);

        return nextVolumes;
    }

    /// <summary>
    /// Ask the store, under whichever of this series' names it answers to, and write what
    /// came back onto the volumes it named.
    /// </summary>
    private async Task<IReadOnlyList<Volume>> EnrichFromCatalogueAsync(
        Series series,
        SyncRun run,
        DateOnly today,
        EnrichmentBudget budget,
        int expectedBonusVolumes,
        CancellationToken cancellationToken)
    {
        var missesBonusVolumes = ExtraVolumes.CountTracked(series, expectedBonusVolumes) < expectedBonusVolumes;

        // A series whose every volume is complete is still worth one store request a night,
        // because the store is where the next volume shows up first (backlog §12; MangaUpdates
        // lagged 86's Vol. 14). That costs one request per series, cached a day and drawn from
        // the store's own allowance.
        var mayGainNextVolumes = NextMainVolumeWriter.MayCreate(series);

        if (budget.Catalogue.IsExhausted
            || (!series.Volumes.Any(VolumeGaps.NeedsAnything) && !missesBonusVolumes && !mayGainNextVolumes))
        {
            return [];
        }

        var match = await catalogueTitleArbiter.AskAsync(series, run, budget.Catalogue, cancellationToken);

        // Only when the release source says the edition has extras at all: the store is trusted
        // with where one sits, not with whether there is one. Created before the facts are
        // written, so a new volume gets its cover, date and subtitle in this pass. The bonus
        // volumes first, so the ones just created count against the same extras.
        if (expectedBonusVolumes > 0)
        {
            bonusVolumeWriter.Create(series, match, run);
            wholeExtraVolumeWriter.Create(series, match, expectedBonusVolumes, run);
        }

        // After the extras, so it starts past whatever they just added; before the facts loop,
        // so a new volume gets its date and cover in this pass (its record is in
        // match.UntrackedMainVolumes, which the loop already reads).
        var nextVolumes = nextMainVolumeWriter.Create(series, match, today, run);

        foreach (var volume in series.Volumes.OrderBy(volume => volume.Number))
        {
            if (!match.ByVolumeNumber.TryGetValue(volume.Number, out var record)
                && !match.BonusVolumes.TryGetValue(volume.Number, out record)
                && !match.UntrackedMainVolumes.TryGetValue(volume.Number, out record))
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

        return nextVolumes;
    }
}
