namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// Applies the release source's word on whether publication has finished.
/// </summary>
// Extracted from SeriesRefresher alongside ReleaseCountWriter, for the same reason: the user
// can now pick a source on a status disagreement, and that rule belongs with the status rule
// rather than in the orchestration.
//
// Publication status belongs to MangaUpdates by the precedence list (CLAUDE.md), and until
// this existed it was written once at add time and never again — a series that finished
// publishing afterwards stayed Ongoing forever.
public sealed class PublicationStatusWriter(IReleaseSource releaseSource, ILogger<PublicationStatusWriter> logger)
{
    public void Apply(Series series, ReleaseSeriesInfo info, SyncRun run)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(info);
        ArgumentNullException.ThrowIfNull(run);

        switch (series.ChoiceFor(SyncDiscrepancyField.Status))
        {
            case { Source: MetadataSourceName.MangaUpdates } choice:
                FollowReleaseSource(series, info, run, choice);
                break;

            case { } choice:
                // AniList chosen: MangaUpdates' status is never written, and the shelf's
                // value stands. The disagreement is still recorded, under today's condition.
                RecordCompletedDisagreement(series, info, run, choice);
                break;

            default:
                ApplyAutomatically(series, info, run);
                break;
        }
    }

    // Only the "finished" direction is written. The source's answer used to be a boolean, and
    // a false cannot tell Ongoing from Hiatus from Cancelled; overwriting AniList's richer
    // value with a guess would lose information the shelf already had. So a disagreement in
    // that direction is recorded for a person and nothing is written.
    private void ApplyAutomatically(Series series, ReleaseSeriesInfo info, SyncRun run)
    {
        if (info.IsCompleted)
        {
            if (series.PublicationStatus is PublicationStatus.Completed)
            {
                return;
            }

            logger.LogInformation(
                "{TitleEnglish}: {SourceName} says publication has finished; {PreviousStatus} becomes Completed",
                series.TitleEnglish,
                releaseSource.SourceName,
                series.PublicationStatus);

            series.PublicationStatus = PublicationStatus.Completed;
            return;
        }

        RecordCompletedDisagreement(series, info, run, choice: null);
    }

    /// <summary>
    /// The user took MangaUpdates' word: its full status is written on every run, so the
    /// shelf follows it when the series later finishes.
    /// </summary>
    private void FollowReleaseSource(Series series, ReleaseSeriesInfo info, SyncRun run, SourceChoice choice)
    {
        if (series.PublicationStatus != info.PublicationStatus)
        {
            logger.LogInformation(
                "{TitleEnglish}: following {SourceName} by your choice; {PreviousStatus} becomes {Status}",
                series.TitleEnglish,
                releaseSource.SourceName,
                series.PublicationStatus,
                info.PublicationStatus);

            series.PublicationStatus = info.PublicationStatus;
        }

        // Nothing displaced is on record for a legacy choice, so there is no disagreement
        // to restate; and agreeing with what was displaced is not one either.
        if (choice.ReplacedValue is { } replacedValue
            && replacedValue != DiscrepancyValueFormat.Status(info.PublicationStatus))
        {
            run.Discrepancies.Add(SyncDiscrepancies.StatusFromChoice(series, info, choice, replacedValue));
        }
    }

    private static void RecordCompletedDisagreement(
        Series series, ReleaseSeriesInfo info, SyncRun run, SourceChoice? choice)
    {
        if (series.PublicationStatus is not PublicationStatus.Completed || info.IsCompleted)
        {
            return;
        }

        run.Discrepancies.Add(SyncDiscrepancies.Status(series, info, choice));
    }
}
