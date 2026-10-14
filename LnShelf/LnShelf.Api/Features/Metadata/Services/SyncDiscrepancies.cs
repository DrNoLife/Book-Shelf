namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// Builds the <see cref="SyncDiscrepancy"/> rows the sync writers record.
/// </summary>
// Extracted from SeriesRefresher so the conflict-precedence rules it encodes (CLAUDE.md's
// "recorded, never silently picked") read as named cases instead of inline object
// initializers buried in the methods that decide what to write. A further disagreement (a
// new field, or a new source) is another static method here, never an edit to how the
// writers call this class.
//
// Each method that can be settled takes the user's SourceChoice for the field, if any. A
// choice replaces the precedence list's answer: ResolvedWith becomes the chosen source and
// the row says so, but it is still recorded every run — the disagreement did not go away.
public static class SyncDiscrepancies
{
    /// <summary>
    /// A series the release source could not confirm — AniList knows it, MangaUpdates would
    /// not match it with enough confidence to link.
    /// </summary>
    public static SyncDiscrepancy SeriesLink(Series series, ReleaseSourceMatch match) =>
        new SyncDiscrepancy()
        {
            Id = Guid.CreateVersion7(),
            SeriesId = series.Id,
            SeriesTitle = series.TitleEnglish,
            Field = SyncDiscrepancyField.SeriesLink,

            // Unresolved: an unmatched series is a question for a person, and this row is
            // how it gets asked.
            ResolvedWith = null,

            Note = $"{match.Outcome}: {match.Explanation}",
        }
            .Says(MetadataSourceName.AniList, series.TitleEnglish)
            .Says(
                MetadataSourceName.MangaUpdates,
                match.Score is { } score ? $"best match scored {score}" : null);

    /// <summary>
    /// A volume-count disagreement — the shelf tracks a different number of volumes than
    /// the release source counts.
    /// </summary>
    public static SyncDiscrepancy VolumeCount(
        Series series, ReleaseSeriesInfo info, int sourceCount, int trackedCount, SourceChoice? choice) =>
        new SyncDiscrepancy()
        {
            Id = Guid.CreateVersion7(),
            SeriesId = series.Id,
            SeriesTitle = series.TitleEnglish,
            Field = SyncDiscrepancyField.VolumeCount,

            // The user's pick, else whichever source the caller actually acted on. A higher
            // count is believed and the volumes are created, so MangaUpdates won; a lower
            // one is recorded and nothing is deleted, so the shelf's AniList-sourced list
            // stands. The row used to say AniList either way, which was a lie in exactly the
            // case where volumes appeared.
            ResolvedWith = choice?.Source ?? (sourceCount > trackedCount
                ? MetadataSourceName.MangaUpdates
                : MetadataSourceName.AniList),
            ResolvedByChoice = choice is not null,

            Note = $"MangaUpdates status: {info.RawStatus}",
        }
            .Says(MetadataSourceName.AniList, DiscrepancyValueFormat.Count(trackedCount))
            .Says(MetadataSourceName.MangaUpdates, DiscrepancyValueFormat.Count(sourceCount));

    /// <summary>
    /// A publication-status disagreement — the shelf already says Completed and the release
    /// source says otherwise.
    /// </summary>
    public static SyncDiscrepancy Status(Series series, ReleaseSeriesInfo info, SourceChoice? choice) =>
        new SyncDiscrepancy()
        {
            Id = Guid.CreateVersion7(),
            SeriesId = series.Id,
            SeriesTitle = series.TitleEnglish,
            Field = SyncDiscrepancyField.Status,

            // Unresolved unless the user picked: nothing was written, and which source is
            // right is a question for a person rather than one a boolean can settle.
            ResolvedWith = choice?.Source,
            ResolvedByChoice = choice is not null,

            Note = $"MangaUpdates status: {info.RawStatus}",
        }
            // AniList names the shelf's stored value's usual provenance, the same way the
            // volume-count row uses it for the shelf's own tracked count.
            .Says(MetadataSourceName.AniList, DiscrepancyValueFormat.Status(PublicationStatus.Completed))
            .Says(MetadataSourceName.MangaUpdates, DiscrepancyValueFormat.Status(info.PublicationStatus));

    /// <summary>
    /// A status the user took from MangaUpdates, differing from the one it displaced.
    /// </summary>
    // A separate shape from Status above because the sides differ: what the choice displaced
    // is whatever the shelf held when it was made (SourceChoice.ReplacedValue), not
    // necessarily Completed.
    public static SyncDiscrepancy StatusFromChoice(
        Series series, ReleaseSeriesInfo info, SourceChoice choice, string replacedValue) =>
        new SyncDiscrepancy()
        {
            Id = Guid.CreateVersion7(),
            SeriesId = series.Id,
            SeriesTitle = series.TitleEnglish,
            Field = SyncDiscrepancyField.Status,
            ResolvedWith = choice.Source,
            ResolvedByChoice = true,
            Note = $"MangaUpdates status: {info.RawStatus}",
        }
            .Says(MetadataSourceName.AniList, replacedValue)
            .Says(MetadataSourceName.MangaUpdates, DiscrepancyValueFormat.Status(info.PublicationStatus));

    /// <summary>
    /// A volume's release date a per-volume source contradicts the count about: the count says
    /// it is out, the source dates it in the future.
    /// </summary>
    /// <param name="source">The source whose date it is.</param>
    /// <param name="note">What the writer did about it, in one sentence.</param>
    public static SyncDiscrepancy ReleaseDate(
        Series series,
        Volume volume,
        MetadataSourceName source,
        DateOnly date,
        string note,
        SourceChoice? choice) =>
        new SyncDiscrepancy()
        {
            Id = Guid.CreateVersion7(),
            SeriesId = series.Id,
            SeriesTitle = series.TitleEnglish,
            Field = SyncDiscrepancyField.ReleaseDate,
            VolumeNumber = volume.Number,
            ResolvedWith = choice?.Source,
            ResolvedByChoice = choice is not null,
            Note = note,
        }
            .Says(MetadataSourceName.MangaUpdates, DiscrepancyValueFormat.InPrint)
            .Says(source, DiscrepancyValueFormat.Date(date));

    /// <summary>
    /// Chapter-map sources starting one volume more than a chapter apart.
    /// </summary>
    // Resolved every time, by the source whose range was written: precedence settles a chapter
    // range, and there is no choice to make (SyncDiscrepancyField.ChapterRange).
    public static SyncDiscrepancy ChapterRange(Series series, ChapterStartDisagreement disagreement)
    {
        var discrepancy = new SyncDiscrepancy
        {
            Id = Guid.CreateVersion7(),
            SeriesId = series.Id,
            SeriesTitle = series.TitleEnglish,
            Field = SyncDiscrepancyField.ChapterRange,
            VolumeNumber = disagreement.VolumeNumber,
            ResolvedWith = disagreement.WrittenSource,
            Note = $"The shelf shows {disagreement.WrittenSource.DisplayName}'s range: the first source that maps a volume supplies it.",
        };

        foreach (var offer in disagreement.Offers)
        {
            discrepancy.Says(offer.Source, DiscrepancyValueFormat.ChapterRange(offer.Range));
        }

        return discrepancy;
    }
}
