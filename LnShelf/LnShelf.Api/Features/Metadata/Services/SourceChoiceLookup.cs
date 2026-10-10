namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// Reading a series' source choices, in the vocabulary the sync writers use.
/// </summary>
public static class SourceChoiceLookup
{
    extension(Series series)
    {
        /// <returns>The user's choice for this field (and volume), or null for "automatic".</returns>
        public SourceChoice? ChoiceFor(SyncDiscrepancyField field, int? volumeNumber = null) =>
            series.SourceChoices.FirstOrDefault(choice =>
                choice.Field == field && choice.VolumeNumber == volumeNumber);

        /// <summary>
        /// Whether the user picked a date source (Apple Books, Open Library) for this volume's
        /// release date — in which case that date, not the count, decides whether it is out.
        /// </summary>
        public bool HasDateSourceChoice(int volumeNumber) =>
            series.ChoiceFor(SyncDiscrepancyField.ReleaseDate, volumeNumber)
                is { Source: not MetadataSourceName.MangaUpdates };
    }
}
