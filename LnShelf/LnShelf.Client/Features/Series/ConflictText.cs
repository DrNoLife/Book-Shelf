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
            SyncDiscrepancyField.ChapterRange => "the chapter range",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown disagreement field."),
        };

        // Without the article, for "Vol 15's release date".
        private string Noun => kind switch
        {
            SyncDiscrepancyField.ReleaseDate => "release date",
            SyncDiscrepancyField.VolumeCount => "volume count",
            SyncDiscrepancyField.Status => "publication status",
            SyncDiscrepancyField.ChapterRange => "chapter range",
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
            { VolumeNumber: { } volumeNumber } => $"Sources disagree on Vol {volumeNumber.ToVolumeText()}'s {conflict.Field.Noun}",
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
        /// The row's subject in the conflict list, which already says "Sources disagree" once in
        /// its title: "Vol 15's release date", "Volume count", "Publication status".
        /// </summary>
        public string Subject => conflict switch
        {
            { Field: SyncDiscrepancyField.SeriesLink } => conflict.Heading,
            { VolumeNumber: { } volumeNumber } => $"Vol {volumeNumber.ToVolumeText()}'s {conflict.Field.Noun}",
            _ => Capitalized(conflict.Field.Noun),
        };

        /// <summary>
        /// "using AniList (automatic)" or "using Apple Books (your choice)" for a settled
        /// disagreement; null for one that still waits on the reader.
        /// </summary>
        public string? SettledNote => conflict is { NeedsReview: false, DisplayedSource: { } source }
            ? $"using {source.DisplayName} ({(conflict.Reason is SourceDisplayReason.YourChoice ? "your choice" : "automatic")})"
            : null;

        /// <summary>
        /// The row's heading: the subject, and who settled it when it is settled —
        /// "Volume count · using AniList (automatic)".
        /// </summary>
        // The settled state rides on the heading so the sentence under it can keep saying what
        // each source said, which is what a reader needs before changing a settled answer.
        public string RowHeading => conflict.SettledNote is { } note
            ? $"{conflict.Subject} · {note}"
            : conflict.Subject;
    }

    /// <summary>"Sources disagree · 3" — the title of the one panel that lists them all.</summary>
    public static string ListTitle(int count) => $"Sources disagree · {count}";

    /// <summary>"Using Apple Books (your choice)" — the quiet note beside an overridden value.</summary>
    public static string ChoiceMarker(MetadataSourceName source) => $"Using {source.DisplayName} (your choice)";

    extension(SeriesDetailResponse series)
    {
        /// <returns>The source the user picked for this field (and volume), or null for automatic.</returns>
        public MetadataSourceName? ChoiceFor(SyncDiscrepancyField field, decimal? volumeNumber = null) =>
            series.SourceChoices
                .FirstOrDefault(choice => choice.Field == field && choice.VolumeNumber == volumeNumber)
                ?.Source;
    }

    private static string Capitalized(string phrase) =>
        phrase.Length == 0 ? phrase : char.ToUpperInvariant(phrase[0]) + phrase[1..];
}
