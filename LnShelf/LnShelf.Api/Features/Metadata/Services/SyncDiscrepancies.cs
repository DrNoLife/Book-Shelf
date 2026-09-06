namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// Builds the <see cref="SyncDiscrepancy"/> rows <see cref="SeriesRefresher"/> records.
/// </summary>
// Extracted from SeriesRefresher so the three conflict-precedence rules it encodes
// (CLAUDE.md's "recorded, never silently picked") read as three named cases instead of three
// inline object initializers buried in the methods that decide what to write. A fourth
// disagreement (a new field, or a new source) is a fourth static method here, never an edit
// to how SeriesRefresher calls this class.
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
        Series series, ReleaseSeriesInfo info, int sourceCount, int trackedCount) =>
        new SyncDiscrepancy()
        {
            Id = Guid.CreateVersion7(),
            SeriesId = series.Id,
            SeriesTitle = series.TitleEnglish,
            Field = SyncDiscrepancyField.VolumeCount,

            // Whichever source the caller actually acted on. A higher count is believed
            // and the volumes are created, so MangaUpdates won; a lower one is recorded and
            // nothing is deleted, so the shelf's AniList-sourced list stands. The row used
            // to say AniList either way, which was a lie in exactly the case where volumes
            // appeared.
            ResolvedWith = sourceCount > trackedCount
                ? MetadataSourceName.MangaUpdates
                : MetadataSourceName.AniList,

            Note = $"MangaUpdates status: {info.RawStatus}",
        }
            .Says(MetadataSourceName.AniList, trackedCount.ToString(CultureInfo.InvariantCulture))
            .Says(MetadataSourceName.MangaUpdates, sourceCount.ToString(CultureInfo.InvariantCulture));

    /// <summary>
    /// A publication-status disagreement — the shelf already says Completed and the release
    /// source's boolean says otherwise, with nothing able to tell Ongoing from Hiatus from
    /// Cancelled.
    /// </summary>
    public static SyncDiscrepancy Status(Series series, ReleaseSeriesInfo info) =>
        new SyncDiscrepancy()
        {
            Id = Guid.CreateVersion7(),
            SeriesId = series.Id,
            SeriesTitle = series.TitleEnglish,
            Field = SyncDiscrepancyField.Status,

            // Unresolved: nothing was written, and which source is right is a question for
            // a person rather than one a boolean can settle.
            ResolvedWith = null,

            Note = $"MangaUpdates status: {info.RawStatus}",
        }
            // AniList names the shelf's stored value's usual provenance, the same way the
            // volume-count row uses it for the shelf's own tracked count.
            .Says(MetadataSourceName.AniList, nameof(PublicationStatus.Completed))
            .Says(MetadataSourceName.MangaUpdates, "not completed");
}
