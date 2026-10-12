using LnShelf.Api.Tests.Features.Metadata.Fixtures;
using static LnShelf.Api.Tests.Features.Metadata.Services.VolumeEnrichmentTestContext;

namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// What a refresh does with MangaUpdates' latest chapter: writes it as metadata, never erases
/// it with an empty answer, and never touches the reader's own chapter.
/// </summary>
// Built the way SeriesRefresherTests builds the refresher, in a file of its own so the
// chapter rule reads as one thing.
public sealed class SeriesRefresherChapterTests
{
    [Fact]
    public async Task RefreshAsync_TheCapturedManga_WritesItsLatestChapter()
    {
        var series = CreateSeries();
        var info = MangaUpdatesSeriesMapper.ToSeriesInfo(
            MangaUpdatesFixtures.LoadSeries(MangaUpdatesFixtures.ChainsawManSeries));

        await RefreshAsync(series, info);

        series.LatestChapter.ShouldBe(232m);
    }

    [Fact]
    public async Task RefreshAsync_TheSourceListsNoChapter_KeepsTheKnownOne()
    {
        var series = CreateSeries();
        series.LatestChapter = 232m;

        await RefreshAsync(series, Info(latestChapter: null));

        series.LatestChapter.ShouldBe(232m);
    }

    [Fact]
    public async Task RefreshAsync_TheSourceMovedOn_WritesTheNewChapter()
    {
        var series = CreateSeries();
        series.LatestChapter = 231m;

        await RefreshAsync(series, Info(latestChapter: 232m));

        series.LatestChapter.ShouldBe(232m);
    }

    // Sync never writes user state (CLAUDE.md): the reader's chapter and unit stay as they were,
    // and running the same refresh twice changes nothing more.
    [Fact]
    public async Task RefreshAsync_TwiceOverAReaderOnChapters_LeavesTheirChapterAlone()
    {
        var series = CreateSeries();
        series.UserState = new SeriesUserState
        {
            SeriesId = series.Id,
            TrackingStatus = TrackingStatus.Reading,
            ProgressUnit = ProgressUnit.Chapters,
            LastChapterRead = 142m,
        };

        await RefreshAsync(series, Info(latestChapter: 232m));
        await RefreshAsync(series, Info(latestChapter: 232m));

        series.LatestChapter.ShouldBe(232m);
        series.UserState.LastChapterRead.ShouldBe(142m);
        series.UserState.ProgressUnit.ShouldBe(ProgressUnit.Chapters);
        series.UserState.TrackingStatus.ShouldBe(TrackingStatus.Reading);
    }

    private static async Task RefreshAsync(Series series, ReleaseSeriesInfo info)
    {
        var run = new SyncRun { Id = Guid.CreateVersion7(), StartedAt = DateTimeOffset.UnixEpoch };

        await CreateRefresher(info).RefreshAsync(series, run, [], new LookupBudget(8), CancellationToken.None);
    }

    private static SeriesRefresher CreateRefresher(ReleaseSeriesInfo info)
    {
        var releaseSource = Substitute.For<IReleaseSource>();
        releaseSource.SourceName.Returns("MangaUpdates");
        releaseSource.GetSeriesAsync(MangaUpdatesFixtures.ChainsawManSeriesId, Arg.Any<CancellationToken>()).Returns(info);

        var metadataSource = Substitute.For<ISeriesMetadataSource>();
        metadataSource.GetSeriesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns((SeriesMetadata?)null);

        return new SeriesRefresher(
            Substitute.For<IReleaseSourceMatcher>(),
            releaseSource,
            metadataSource,
            new ReleaseCountWriter(releaseSource, NullLogger<ReleaseCountWriter>.Instance),
            new PublicationStatusWriter(releaseSource, NullLogger<PublicationStatusWriter>.Instance),
            VolumeEnrichmentPipeline.Create(
                NoCatalogue(),
                NoEnrichment(),
                NoCandidates(),
                VolumeEnrichmentPipeline.Writer()),
            TimeProvider.System,
            NullLogger<SeriesRefresher>.Instance);
    }

    private static ReleaseSeriesInfo Info(decimal? latestChapter) => new()
    {
        ExternalId = MangaUpdatesFixtures.ChainsawManSeriesId,
        Title = "Chainsaw Man",
        VolumeCount = 2,
        PublicationStatus = PublicationStatus.Ongoing,
        RawStatus = "2 Volumes (Ongoing)",
        LatestChapter = latestChapter,
    };

    // Linked already, and swept before, so the refresh re-reads the record rather than matching.
    private static Series CreateSeries()
    {
        var series = new Series
        {
            Id = Guid.CreateVersion7(),
            MangaUpdatesId = MangaUpdatesFixtures.ChainsawManSeriesId,
            TitleEnglish = "Chainsaw Man",
            Format = Format.Manga,
            PublicationStatus = PublicationStatus.Ongoing,
            LastSyncedAt = DateTimeOffset.UnixEpoch,
        };

        foreach (var number in Enumerable.Range(1, 2))
        {
            series.Volumes.Add(new Volume { Id = Guid.CreateVersion7(), Number = number, IsReleased = true });
        }

        return series;
    }
}
