using LnShelf.Api.Tests.Features.Metadata.Fixtures;

namespace LnShelf.Api.Tests.Features.Metadata.Mappers;

/// <summary>
/// MangaUpdates' <c>latest_chapter</c>, dirty → clean, against captured records (testing.md #4).
/// </summary>
// Its own file beside MangaUpdatesSeriesMapperTests, which is about titles and counts.
public sealed class MangaUpdatesSeriesMapperChapterTests
{
    [Theory]
    [InlineData(MangaUpdatesFixtures.ChainsawManSeries, 232)]
    [InlineData(MangaUpdatesFixtures.FremdTorturchenSeries, 6)]
    [InlineData(MangaUpdatesFixtures.YoujoSenkiSeries, 5)]
    public void ToSeriesInfo_TheCapturedSeries_CarriesTheLatestChapter(string fixture, int expected) =>
        MangaUpdatesSeriesMapper.ToSeriesInfo(MangaUpdatesFixtures.LoadSeries(fixture))
            .LatestChapter.ShouldBe(expected);

    [Fact]
    public void ToSeriesInfo_TheCapturedManga_IsTheRecordTheShelfAsksFor()
    {
        var info = MangaUpdatesSeriesMapper.ToSeriesInfo(
            MangaUpdatesFixtures.LoadSeries(MangaUpdatesFixtures.ChainsawManSeries));

        info.ExternalId.ShouldBe(MangaUpdatesFixtures.ChainsawManSeriesId);
        info.Title.ShouldBe("Chainsaw Man");
        info.SourceType.ShouldBe("Manga");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public void ToSeriesInfo_NoChapterOrZero_CarriesNone(int? latestChapter)
    {
        var record = MangaUpdatesFixtures.LoadSeries(MangaUpdatesFixtures.ChainsawManSeries);
        record.LatestChapter = latestChapter;

        MangaUpdatesSeriesMapper.ToSeriesInfo(record).LatestChapter.ShouldBeNull();
    }
}
