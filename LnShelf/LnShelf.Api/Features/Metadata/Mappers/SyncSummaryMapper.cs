namespace LnShelf.Api.Features.Metadata.Mappers;

/// <summary>Sync entities → the wire contract the sidebar and review list read.</summary>
public static class SyncSummaryMapper
{
    extension(SyncRun run)
    {
        /// <param name="choices">
        /// The user's current choices: a disagreement one of them covers reads as settled by
        /// it, and a recorded choice that has since been reset reads as unresolved again.
        /// </param>
        public SyncSummaryResponse ToResponse(IReadOnlyCollection<SourceChoice> choices) => new()
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
            DescriptionsWritten = run.DescriptionsWritten,
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
                    // One entry per (series, field, volume), merged the way the series page reads
                    // them, so the sidebar's "to review" count and the list agree.
                    .GroupBy(discrepancy => (discrepancy.SeriesId, discrepancy.Field, discrepancy.VolumeNumber))
                    .Select(rows => RecordedDisagreement.FromRows(rows, run.StartedAt).Discrepancy)
                    .Select(discrepancy => discrepancy.ToResponse(
                        choices.FirstOrDefault(choice => choice.Covers(discrepancy))))
                    .OrderBy(response => response.ResolvedWith is not null)
                    .ThenBy(response => response.SeriesTitle, StringComparer.OrdinalIgnoreCase),
            ],
        };
    }

    extension(SyncDiscrepancy discrepancy)
    {
        public SyncDiscrepancyResponse ToResponse(SourceChoice? choice = null) => new()
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
            ResolvedWith = discrepancy.ResolutionUnder(choice).Source,
            ResolvedByChoice = discrepancy.ResolutionUnder(choice).ByChoice,
            VolumeNumber = discrepancy.VolumeNumber,
            Note = discrepancy.Note,
        };
    }
}
