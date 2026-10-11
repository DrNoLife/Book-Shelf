namespace LnShelf.Client.Features.Series;

/// <summary>
/// "How is this counted?": where the series' volume count came from, in plain words.
/// </summary>
// Transparent, not magical (design.md's voice rules): the count decides what is "out" and so
// what is "to buy", and a reader who doubts it should be able to see the very text it was read
// from rather than trust a number.
public static class CountProvenanceText
{
    private const string NotLinkedSentence =
        "Counted from AniList's volume list. This series isn't linked to MangaUpdates yet.";

    extension(SeriesDetailResponse series)
    {
        /// <summary>One sentence per fact, in reading order. Never empty.</summary>
        public IReadOnlyList<string> CountExplanation
        {
            get
            {
                List<string> lines = [series.CountSourceSentence + series.CheckedClause];

                if (series.CatalogueBonusVolumes is { } bonusVolumes)
                {
                    lines.Add(bonusVolumes);
                }

                return lines;
            }
        }

        private string CountSourceSentence
        {
            get
            {
                var provenance = series.CountProvenance;

                // The reader's own pick wins over every basis below, so it is said first and
                // plainly: the shelf is not using the number the source gave.
                if (series.ChoiceFor(SyncDiscrepancyField.VolumeCount) is MetadataSourceName.AniList)
                {
                    return provenance.Evidence is { } quoted
                        ? $"You chose AniList's volume list, so every volume tracked here counts as out. MangaUpdates says “{quoted}”, which is not used."
                        : "You chose AniList's volume list, so every volume tracked here counts as out.";
                }

                return provenance.Basis switch
                {
                    VolumeCountBasis.EnglishPublisherNote => provenance.Evidence is { } note
                        ? $"Counted from MangaUpdates, the English publisher's note: “{note}”."
                        : "Counted from MangaUpdates, an English publisher's note.",
                    VolumeCountBasis.MangaUpdatesStatus => provenance.Evidence is { } status
                        ? $"Counted from MangaUpdates' status line, which counts the Japanese edition: “{status}”. No English publisher states a count."
                        : "Counted from MangaUpdates' status line, which counts the Japanese edition.",
                    // An AniList basis on an unlinked series means MangaUpdates was never asked,
                    // not that it had no count.
                    VolumeCountBasis.AniList when series.MangaUpdatesId is null => NotLinkedSentence,
                    VolumeCountBasis.AniList => "Counted from AniList's volume list. MangaUpdates gives no count.",
                    VolumeCountBasis.Manual => "Added by hand, so the volumes are the ones you added.",
                    _ when series.MangaUpdatesId is null => NotLinkedSentence,
                    _ => "Counted from MangaUpdates. The exact text it was read from is recorded from the next sync on.",
                };
            }
        }

        /// <summary>" Checked 11 Oct." — when the sync last read the count; nothing for a hand-made series.</summary>
        private string CheckedClause =>
            series.CountProvenance is { CheckedAt: { } checkedAt, Basis: not VolumeCountBasis.Manual }
                ? $" Checked {DateOnly.FromDateTime(checkedAt.ToLocalTime().DateTime).Short}."
                : string.Empty;

        /// <summary>
        /// "Vol 7.5 found in the Apple Books catalogue." — the bonus volumes no count includes,
        /// which only the store catalogue creates. Null when there are none.
        /// </summary>
        private string? CatalogueBonusVolumes
        {
            get
            {
                var numbers = series.Volumes
                    .Where(volume => volume.Number.IsBonusVolume && !volume.IsLocalOnly)
                    .Select(volume => volume.Number.ToVolumeText())
                    .ToList();

                return numbers.Count switch
                {
                    0 => null,
                    1 => $"Vol {numbers[0]} found in the {MetadataSourceName.AppleBooks.DisplayName} catalogue.",
                    _ => $"Vols {string.Join(", ", numbers[..^1])} and {numbers[^1]} found in the {MetadataSourceName.AppleBooks.DisplayName} catalogue.",
                };
            }
        }
    }

    extension(VolumeResponse volume)
    {
        /// <summary>
        /// "Date from Open Library" — which per-volume source wrote the date. Null when there is no
        /// date or its source was not recorded.
        /// </summary>
        public string? DateSourceLine => volume is { ReleaseDate: not null, ReleaseDateSource: { } source }
            ? $"Date from {source.DisplayName}"
            : null;
    }
}
