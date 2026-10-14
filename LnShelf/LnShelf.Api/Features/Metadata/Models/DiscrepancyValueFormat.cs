namespace LnShelf.Api.Features.Metadata.Models;

/// <summary>
/// How a source's side of a disagreement is written into <see cref="SyncDiscrepancyValue"/>
/// and read back.
/// </summary>
// The strings were written ad hoc in three places. Choosing a source means acting on what
// the diff showed, so the applier has to read them back, and a format that two writers and a
// reader each spell on their own is a format that drifts. One home for write and parse.
public static class DiscrepancyValueFormat
{
    /// <summary>What the release source says about a volume it counts: out, with no date.</summary>
    public const string InPrint = "in print";

    private const string DateFormat = "yyyy-MM-dd";

    public static string Date(DateOnly date) => date.ToString(DateFormat, CultureInfo.InvariantCulture);

    public static string Count(int count) => count.ToString(CultureInfo.InvariantCulture);

    public static string Status(PublicationStatus status) => status.ToString();

    /// <summary>"chapters 223–232", or "chapters from 230" while the volume's end is unknown.</summary>
    // Written to be read, never parsed back: a chapter range has no choice to act on.
    public static string ChapterRange(VolumeChapterRange range) => range.LastChapter is { } last
        ? $"chapters {Chapter(range.FirstChapter)}–{Chapter(last)}"
        : $"chapters from {Chapter(range.FirstChapter)}";

    private static string Chapter(decimal chapter) => chapter.ToString("0.##", CultureInfo.InvariantCulture);

    public static bool TryParseDate(string? text, out DateOnly date) =>
        DateOnly.TryParseExact(text, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    public static bool TryParseCount(string? text, out int count) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out count);

    // Enum.TryParse also accepts numbers and comma lists; only a defined name is a status here.
    public static bool TryParseStatus(string? text, out PublicationStatus status)
    {
        status = default;

        return !string.IsNullOrEmpty(text)
               && !char.IsDigit(text[0])
               && Enum.TryParse(text, ignoreCase: false, out status)
               && Enum.IsDefined(status);
    }
}
