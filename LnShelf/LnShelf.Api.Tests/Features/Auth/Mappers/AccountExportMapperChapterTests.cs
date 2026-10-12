using LnShelf.Api.Features.Auth.Mappers;

namespace LnShelf.Api.Tests.Features.Auth.Mappers;

/// <summary>
/// "Export my data" carries chapter progress: the reader's unit and chapter, and the source's
/// latest chapter beside the other catalogue facts.
/// </summary>
public sealed class AccountExportMapperChapterTests
{
    [Fact]
    public void ToSeries_AChaptersSeries_ExportsTheUnitAndBothChapters()
    {
        var series = CreateSeries();
        series.UserState = new SeriesUserState
        {
            SeriesId = series.Id,
            TrackingStatus = TrackingStatus.Reading,
            ProgressUnit = ProgressUnit.Chapters,
            LastChapterRead = 142.5m,
        };

        var exported = AccountExportMapper.ToSeries(series, []);

        exported.ProgressUnit.ShouldBe(ProgressUnit.Chapters);
        exported.LastChapterRead.ShouldBe(142.5m);
        exported.LatestChapter.ShouldBe(232m);
    }

    [Fact]
    public void ToSeries_ASeriesWithNoStateRow_ExportsVolumesAndNoChapter()
    {
        var exported = AccountExportMapper.ToSeries(CreateSeries(), []);

        exported.ProgressUnit.ShouldBe(ProgressUnit.Volumes);
        exported.LastChapterRead.ShouldBeNull();
    }

    private static Series CreateSeries() => new()
    {
        Id = Guid.CreateVersion7(),
        TitleEnglish = "Chainsaw Man",
        Format = Format.Manga,
        PublicationStatus = PublicationStatus.Ongoing,
        LatestChapter = 232m,
    };
}
