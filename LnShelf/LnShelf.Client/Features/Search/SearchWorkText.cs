namespace LnShelf.Client.Features.Search;

/// <summary>
/// The words the work cards print about formats: the row suffix and the header's count line.
/// </summary>
public static class SearchWorkText
{
    private static readonly Format[] FormatOrder = [Format.LightNovel, Format.Manga, Format.WebNovel];

    /// <summary>"LN", "Manga", "WN" — the format badge's words, for a row's " · LN" suffix.</summary>
    public static string ShortName(Format format) => format switch
    {
        Format.LightNovel => "LN",
        Format.Manga => "Manga",
        Format.WebNovel => "WN",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown format."),
    };

    /// <summary>"Light novel", "Manga", "Web novel" — a row's name when its title is the work's.</summary>
    public static string LongName(Format format) => format switch
    {
        Format.LightNovel => "Light novel",
        Format.Manga => "Manga",
        Format.WebNovel => "Web novel",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown format."),
    };

    /// <summary>"8 light novel series · 4 manga series".</summary>
    public static string Summary(SearchWork work)
    {
        ArgumentNullException.ThrowIfNull(work);

        return string.Join(" · ", CountedFormats(work).Select(counted => $"{counted.Count} {LongNoun(counted.Format)} series"));
    }

    /// <summary>"8 LN · 4 manga series": the same count, short enough for a phone's card.</summary>
    public static string ShortSummary(SearchWork work)
    {
        ArgumentNullException.ThrowIfNull(work);

        var counted = CountedFormats(work).ToList();

        // One format fits in full ("4 light novel series"); only the mixed line needs shortening.
        if (counted.Count <= 1)
        {
            return Summary(work);
        }

        return $"{string.Join(" · ", counted.Select(format => $"{format.Count} {ShortNoun(format.Format)}"))} series";
    }

    private static IEnumerable<(Format Format, int Count)> CountedFormats(SearchWork work) =>
        FormatOrder
            .Select(format => (Format: format, Count: work.CountOf(format)))
            .Where(counted => counted.Count > 0);

    private static string LongNoun(Format format) => format switch
    {
        Format.LightNovel => "light novel",
        Format.Manga => "manga",
        Format.WebNovel => "web novel",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown format."),
    };

    private static string ShortNoun(Format format) => format switch
    {
        Format.LightNovel => "LN",
        Format.Manga => "manga",
        Format.WebNovel => "WN",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown format."),
    };
}
