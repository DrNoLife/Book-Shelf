namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// The shelf's format in MangaUpdates' vocabulary — one answer, where the release source and
/// the add-flow search each used to have their own and they had drifted apart.
/// </summary>
public sealed class MangaUpdatesFormatsTests
{
    [Fact]
    public void ToSourceTypes_LightNovel_AsksForNovelsOnly() =>
        // An unfiltered search for "Mushoku Tensei" returns thousands of hits led by
        // doujinshi; restricted to Novel the real series is on the first page.
        MangaUpdatesFormats.ToSourceTypes(Format.LightNovel).ShouldBe(["Novel"]);

    [Fact]
    public void ToSourceTypes_Manga_AsksForManhwaAndManhuaToo()
    {
        // The divergence this file exists for. The release source asked only for "Manga",
        // while MangaUpdatesSeriesMapper.ToFormat reads all three of these as Format.Manga —
        // so a manhwa the add flow could add was one the release source could never link.
        MangaUpdatesFormats.ToSourceTypes(Format.Manga).ShouldBe(["Manga", "Manhwa", "Manhua"]);
    }

    [Fact]
    public void ToSourceTypes_WebNovel_AsksForNovels() =>
        // MangaUpdates does not file web novels apart from novels; "Novel" is as close as its
        // vocabulary gets. Only the release source ever asks — the search returns early.
        MangaUpdatesFormats.ToSourceTypes(Format.WebNovel).ShouldBe(["Novel"]);

    [Fact]
    public void ToSourceTypes_NoFormatAtAll_AsksForEveryTrackedMedium() =>
        // An unnarrowed add-flow search: every medium the shelf can track, and nothing else —
        // artbooks and doujinshi are still filtered out at the request.
        MangaUpdatesFormats.ToSourceTypes(format: null)
            .ShouldBe(["Novel", "Manga", "Manhwa", "Manhua"]);

    [Theory]
    [InlineData(Format.LightNovel)]
    [InlineData(Format.Manga)]
    [InlineData(Format.WebNovel)]
    public void ToSourceTypes_EveryFormat_AsksOnlyForTypesTheMapperCanRead(Format format)
    {
        // The invariant the two copies broke: asking for a type the mapper cannot turn back
        // into a Format wastes a request, and not asking for one it can loses a series.
        var trackable = MangaUpdatesFormats.ToSourceTypes(format: null);

        MangaUpdatesFormats.ToSourceTypes(format).ShouldBeSubsetOf(trackable);
    }
}
