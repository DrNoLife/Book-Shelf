namespace LnShelf.Client.Features.Series;

/// <summary>
/// The words the series page uses for a disagreement between sources, in one place.
/// </summary>
// The page used to print the contract's own enum name ("Sources disagree on ReleaseDate"),
// which is a code identifier and not a thing to say to a reader, and left the volume out of
// the heading when the field was per volume. Here so the panel, its tests and the quiet
// markers elsewhere on the page cannot word one disagreement three ways.
public static class ConflictText
{
    extension(SyncDiscrepancyField kind)
    {
        /// <summary>The field as a phrase that follows "on": "the volume count", "publication status".</summary>
        public string FieldName => kind switch
        {
            SyncDiscrepancyField.VolumeCount => "the volume count",
            SyncDiscrepancyField.ReleaseDate => "the release date",
            SyncDiscrepancyField.Status => "publication status",
            SyncDiscrepancyField.SeriesLink => "which series this is",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown disagreement field."),
        };

        // Without the article, for "Vol 15's release date".
        private string Noun => kind switch
        {
            SyncDiscrepancyField.ReleaseDate => "release date",
            SyncDiscrepancyField.VolumeCount => "volume count",
            SyncDiscrepancyField.Status => "publication status",
            _ => "series link",
        };
    }

    extension(SourceConflictResponse conflict)
    {
        /// <summary>
        /// "Sources disagree on Vol 15's release date", or "…on the volume count" when it is
        /// about the series.
        /// </summary>
        // Per-volume dates made the volume half necessary: "MangaUpdates says in print, Apple
        // Books says 9 Mar 2027" is unreadable without knowing which volume it is about.
        public string Heading => conflict switch
        {
            { Field: SyncDiscrepancyField.SeriesLink } => "Not linked to a release source yet",
            { VolumeNumber: { } volumeNumber } => $"Sources disagree on Vol {volumeNumber}'s {conflict.Field.Noun}",
            _ => $"Sources disagree on {conflict.Field.FieldName}",
        };

        /// <summary>
        /// What each source said, in one line: "MangaUpdates says it's in print. Apple Books
        /// says 9 Mar 2027."
        /// </summary>
        public string Sentence => conflict.Field is SyncDiscrepancyField.SeriesLink
            ? "Waiting on you."
            : string.Join(" ", conflict.Options.Select(option =>
                $"{option.SourceName} says {(option.DisplayValue == "in print" ? "it's in print" : option.DisplayValue)}."));

        /// <summary>
        /// The line a settled disagreement shows where the open one shows its sentence.
        /// </summary>
        public string SettledLine => conflict.DisplayedSource is { } source
            ? conflict.Reason is SourceDisplayReason.YourChoice
                ? ChoiceMarker(source)
                : $"Using {source.DisplayName} (automatic)"
            : conflict.Sentence;
    }

    /// <summary>"Using Apple Books (your choice)" — the quiet note beside an overridden value.</summary>
    public static string ChoiceMarker(MetadataSourceName source) => $"Using {source.DisplayName} (your choice)";

    extension(SeriesDetailResponse series)
    {
        /// <returns>The source the user picked for this field (and volume), or null for automatic.</returns>
        public MetadataSourceName? ChoiceFor(SyncDiscrepancyField field, int? volumeNumber = null) =>
            series.SourceChoices
                .FirstOrDefault(choice => choice.Field == field && choice.VolumeNumber == volumeNumber)
                ?.Source;
    }
}
