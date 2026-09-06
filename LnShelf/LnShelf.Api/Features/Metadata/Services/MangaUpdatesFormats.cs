namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// The shelf's format expressed in MangaUpdates' own vocabulary.
/// </summary>
// Filtering matters more here than it did for AniList: an unfiltered search for "Mushoku
// Tensei" returns 5 872 hits led by doujinshi, and restricted to Novel it returns 172 with
// the real series on the first page.
//
// One copy, where the search query service and the release source each had their own and they
// had drifted apart. The release source asked only for "Manga" while the query service asked
// for all three of Manga, Manhwa and Manhua — and MangaUpdatesSeriesMapper.ToFormat reads all
// three as Format.Manga. The net effect was that a manhwa the add flow could add was a series
// the release source could never link. The wider set is the correct one, because it is the one
// that agrees with the mapper.
public static class MangaUpdatesFormats
{
    private static readonly string[] NovelTypes = ["Novel"];
    private static readonly string[] MangaTypes = ["Manga", "Manhwa", "Manhua"];
    private static readonly string[] AllTrackedTypes = ["Novel", "Manga", "Manhwa", "Manhua"];

    /// <param name="format">Null means "any tracked medium" — an unnarrowed search.</param>
    public static string[] ToSourceTypes(Format? format) => format switch
    {
        Format.LightNovel => NovelTypes,
        Format.Manga => MangaTypes,

        // A web novel is not something MangaUpdates files separately; "Novel" is as close as
        // its vocabulary gets. Only the release source ever asks this — the search query
        // service answers a WebNovel search without a request at all.
        Format.WebNovel => NovelTypes,

        null => AllTrackedTypes,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown format."),
    };
}
