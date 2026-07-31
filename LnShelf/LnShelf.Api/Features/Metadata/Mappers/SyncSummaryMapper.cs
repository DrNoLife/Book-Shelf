namespace LnShelf.Api.Features.Metadata.Mappers;

/// <summary>Sync entities → the wire contract the sidebar and review list read.</summary>
public static class SyncSummaryMapper
{
    extension(SyncRun run)
    {
        public SyncSummaryResponse ToResponse() => new()
        {
            RunId = run.Id,
            StartedAt = run.StartedAt,
            FinishedAt = run.FinishedAt,
            SeriesRefreshed = run.SeriesRefreshed,
            SeriesLinked = run.SeriesLinked,
            DatesMoved = run.DatesMoved,
            VolumesMarkedReleased = run.VolumesMarkedReleased,
            VolumesEnriched = run.VolumesEnriched,
            VolumeCoversCached = run.VolumeCoversCached,
            VolumesResolvedBySearch = run.VolumesResolvedBySearch,
            EnrichmentGaps = run.EnrichmentGaps,
            NewVolumes = run.NewVolumes,

            // One number for both kinds: a series that failed to refresh and a volume that
            // failed to enrich are the same thing to whoever reads the footer.
            Failures = run.Failures + run.EnrichmentFailures,
            Summary = run.Summary,

            // Unresolved first: those are the ones asking for something.
            Discrepancies =
            [
                .. run.Discrepancies
                    .OrderBy(discrepancy => discrepancy.ResolvedWith is not null)
                    .ThenBy(discrepancy => discrepancy.SeriesTitle, StringComparer.OrdinalIgnoreCase)
                    .Select(discrepancy => discrepancy.ToResponse()),
            ],
        };
    }

    extension(SyncDiscrepancy discrepancy)
    {
        public SyncDiscrepancyResponse ToResponse() => new()
        {
            SeriesId = discrepancy.SeriesId,
            SeriesTitle = discrepancy.SeriesTitle,
            Field = discrepancy.Field,

            // Ordered by source rather than left in whatever order the store returns.
            // Insertion order is not a thing a table promises, and the migration that
            // moved these out of two named columns did not preserve one either — so the
            // sentence has to be made stable rather than assumed to be.
            Values =
            [
                .. discrepancy.Values
                    .OrderBy(value => value.Source)
                    .Select(value => new SourcedValueResponse
                    {
                        Source = value.Source,
                        Value = value.Value,
                    }),
            ],
            ResolvedWith = discrepancy.ResolvedWith,
            VolumeNumber = discrepancy.VolumeNumber,
            Note = discrepancy.Note,
        };
    }
}
