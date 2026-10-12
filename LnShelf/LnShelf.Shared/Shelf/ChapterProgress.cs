namespace LnShelf.Shared.Shelf;

/// <summary>
/// The rules for chapter progress — where the reader is between volumes — shared so the
/// series page's input and the API's refusal cannot drift apart, as <see cref="ReadDateLimits"/>
/// does for read dates.
/// </summary>
// Chapter progress is one number per series and nothing more: the shelf does not know which
// volume a chapter belongs to (that mapping is a later layer), so it never moves a volume's
// read status, and To buy / Backlog / Behind stay volume-based.
public static class ChapterProgress
{
    /// <summary>Anything above this is a typo, not a chapter: the longest-running manga are near 1,200.</summary>
    public const decimal MaximumChapter = 100_000m;

    /// <summary>"10.5" is a real chapter number; "10.555" is not.</summary>
    public const int MaximumDecimalPlaces = 2;

    /// <summary>
    /// Whether a series of this format may be set to <see cref="ProgressUnit.Chapters"/>. Manga
    /// and web novels are read chapter by chapter; a light novel is read and bought by the volume.
    /// </summary>
    public static bool IsOfferedFor(Format format) => format is Format.Manga or Format.WebNovel;

    /// <returns>The reason this chapter cannot be saved, or null when it can.</returns>
    public static string? ProblemWith(decimal chapter) =>
        chapter < 0 ? "A chapter number cannot be negative."
        : chapter > MaximumChapter ? $"A chapter number can be at most {MaximumChapter.ToVolumeText()}."
        : decimal.Round(chapter, MaximumDecimalPlaces) != chapter ? "A chapter number can have at most two decimals."
        : null;

    /// <summary>
    /// How many chapters are out that the reader has not read: the latest chapter the release
    /// source knows of minus the last one read.
    /// </summary>
    // Derived, never stored (CLAUDE.md). Null rather than zero whenever either side is
    // unknown, and when the reader is level with or ahead of the source — MangaUpdates lags
    // a simulpub by days, and "-2 to read" would be a claim about chapters nobody counted.
    public static decimal? ToRead(decimal? latestChapter, decimal? lastChapterRead) =>
        latestChapter is { } latest && lastChapterRead is { } lastRead && latest - lastRead > 0
            ? latest - lastRead
            : null;

    /// <summary>
    /// Whether the reader has read up to the latest chapter the release source knows of.
    /// </summary>
    // Only when both numbers are known: with no chapter recorded the reader has not started
    // counting, and with no latest chapter nobody can say they have caught up.
    public static bool IsCaughtUp(decimal? latestChapter, decimal? lastChapterRead) =>
        latestChapter is { } latest && lastChapterRead is { } lastRead && lastRead >= latest;
}
