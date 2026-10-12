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
}
