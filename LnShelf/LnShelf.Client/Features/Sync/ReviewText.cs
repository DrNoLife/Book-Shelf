using LnShelf.Client.Features.Series;

namespace LnShelf.Client.Features.Sync;

/// <summary>
/// The words the review list uses for one open question the last sync left.
/// </summary>
// The series page's wording (ConflictText) for the field names, so a question reads the same
// on the list and on the page it links to.
public static class ReviewText
{
    extension(SyncSummaryResponse run)
    {
        /// <summary>The questions still waiting on the reader — the sidebar's "N to review".</summary>
        public IReadOnlyList<SyncDiscrepancyResponse> ToReview =>
            [.. run.Discrepancies.Where(discrepancy => discrepancy.NeedsReview)];
    }

    extension(SyncDiscrepancyResponse discrepancy)
    {
        /// <summary>"Not linked to a release source yet", "Vol 8: sources disagree on the release date".</summary>
        public string Question => discrepancy switch
        {
            { Field: SyncDiscrepancyField.SeriesLink } => "Not linked to a release source yet",
            { VolumeNumber: { } number } => $"Vol {number.ToVolumeText()}: sources disagree on {discrepancy.Field.FieldName}",
            _ => $"Sources disagree on {discrepancy.Field.FieldName}",
        };

        /// <summary>
        /// What each source said, with the sources' own names: "AniList says 18. MangaUpdates says 16."
        /// The link question asks for a search instead.
        /// </summary>
        public string Said => discrepancy.Field is SyncDiscrepancyField.SeriesLink
            ? "Find it on MangaUpdates from the series page."
            : string.Join(" ", discrepancy.Values.Select(value =>
                $"{value.Source.DisplayName} says {value.Value ?? "nothing"}."));
    }
}
