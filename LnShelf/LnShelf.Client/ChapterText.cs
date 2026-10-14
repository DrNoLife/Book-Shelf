namespace LnShelf.Client;

/// <summary>
/// The chapter lines the shelf prints — "Ch. 142 of 232", "90 to read" — in one place, for the
/// series page and the library alike.
/// </summary>
// Numbers go through ToVolumeText, which is a chapter's rule too: invariant, no trailing zeros,
// so 10.5 is "10.5" in a Danish browser and 142 never comes back as "142.00".
public static class ChapterText
{
    /// <summary>"Ch. 142 of 232", or "Ch. 142" when no source knows the latest chapter.</summary>
    public static string Position(decimal lastChapterRead, decimal? latestChapter) =>
        latestChapter is { } latest && latest >= lastChapterRead
            ? $"Ch. {lastChapterRead.ToVolumeText()} of {latest.ToVolumeText()}"
            : $"Ch. {lastChapterRead.ToVolumeText()}";

    /// <summary>"90 to read", "1 to read", or null when nothing is known to be waiting.</summary>
    public static string? ToRead(decimal? latestChapter, decimal? lastChapterRead) =>
        ChapterProgress.ToRead(latestChapter, lastChapterRead) is { } toRead
            ? $"{toRead.ToVolumeText()} to read"
            : null;

    /// <summary>
    /// The library's chapter line for a card, or null when the series is counted in volumes or
    /// no chapter has been recorded yet — in which case the card says nothing about chapters.
    /// </summary>
    public static string? ForCard(LibraryCardResponse card)
    {
        ArgumentNullException.ThrowIfNull(card);

        if (card is not { ProgressUnit: ProgressUnit.Chapters, LastChapterRead: { } lastRead })
        {
            return null;
        }

        return ToRead(card.LatestChapter, lastRead) is { } toRead
            ? $"{Position(lastRead, card.LatestChapter)} · {toRead}"
            : Position(lastRead, card.LatestChapter);
    }

    /// <summary>"Ch. 101 · Vol 12" when a chapter map places the chapter, else just "Ch. 101".</summary>
    public static string WithVolume(string position, decimal lastChapterRead, IEnumerable<VolumeResponse> volumes) =>
        ChapterProgress.VolumeContaining(lastChapterRead, volumes) is { } volumeNumber
            ? $"{position} · Vol {volumeNumber.ToVolumeText()}"
            : position;

    /// <summary>A volume's chapters: "Ch. 98–103", "Ch. 138–" while it is open, null without a range.</summary>
    public static string? Range(VolumeResponse volume)
    {
        ArgumentNullException.ThrowIfNull(volume);

        if (volume.FirstChapter is not { } first)
        {
            return null;
        }

        return volume.LastChapter is { } last
            ? $"Ch. {first.ToVolumeText()}–{last.ToVolumeText()}"
            : $"Ch. {first.ToVolumeText()}–";
    }

    /// <summary>"You've finished Vol 12 — mark it read?", or "Vols 10–12 … mark them read?".</summary>
    public static string FinishedOffer(IReadOnlyList<decimal> volumeNumbers)
    {
        ArgumentNullException.ThrowIfNull(volumeNumbers);

        return volumeNumbers.Count == 1
            ? $"You've finished {VolumesLabel(volumeNumbers)} — mark it read?"
            : $"You've finished {VolumesLabel(volumeNumbers)} — mark them read?";
    }

    /// <summary>
    /// "Vol 12", "Vols 10–12" for a run, "Vols 10, 12" otherwise — design.md's volume words.
    /// </summary>
    public static string VolumesLabel(IReadOnlyList<decimal> volumeNumbers)
    {
        ArgumentNullException.ThrowIfNull(volumeNumbers);

        var numbers = volumeNumbers.Order().ToList();

        if (numbers.Count == 1)
        {
            return $"Vol {numbers[0].ToVolumeText()}";
        }

        var isRun = numbers.Zip(numbers.Skip(1)).All(pair => pair.Second == pair.First + 1);

        return isRun
            ? $"Vols {numbers[0].ToVolumeText()}–{numbers[^1].ToVolumeText()}"
            : $"Vols {string.Join(", ", numbers.Select(number => number.ToVolumeText()))}";
    }
}
