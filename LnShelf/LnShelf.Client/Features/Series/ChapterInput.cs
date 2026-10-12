namespace LnShelf.Client.Features.Series;

/// <summary>
/// What the chapter input shows and accepts, in one place — as <see cref="ReadDateInput"/> is
/// for the read date.
/// </summary>
// A text input with inputmode="decimal" rather than type="number": a number input in a Danish
// browser hands back "" for "10.5" and "10,5" alike depending on the keyboard, and spins on a
// scroll wheel. Text is read here, accepting either separator.
public static class ChapterInput
{
    /// <summary>The input's value: "142", "10.5", or empty for no chapter.</summary>
    public static string Format(decimal? chapter) => chapter?.ToVolumeText() ?? string.Empty;

    /// <returns>Null for an empty or unreadable value — nothing the reader meant as a chapter.</returns>
    public static decimal? Parse(string? text)
    {
        var trimmed = text?.Trim().Replace(',', '.');

        return decimal.TryParse(trimmed, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var chapter)
            ? chapter
            : null;
    }

    /// <summary>
    /// What "+1" records: the next whole chapter. From 10.5 that is 11 — the half chapter was
    /// an extra, and the reader's next one is the next whole number.
    /// </summary>
    public static decimal Next(decimal? lastChapterRead) => decimal.Floor(lastChapterRead ?? 0) + 1;
}
