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
            NewVolumes = run.NewVolumes,
            Failures = run.Failures,
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
            AniListValue = discrepancy.AniListValue,
            MangaUpdatesValue = discrepancy.MangaUpdatesValue,
            ResolvedWith = discrepancy.ResolvedWith,
            VolumeNumber = discrepancy.VolumeNumber,
            Note = discrepancy.Note,
        };
    }
}
