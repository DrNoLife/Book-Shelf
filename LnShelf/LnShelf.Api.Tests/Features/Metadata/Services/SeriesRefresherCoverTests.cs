using static LnShelf.Api.Tests.Features.Metadata.Services.VolumeEnrichmentTestContext;

namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// The series cover heal: a series whose cover could not be cached when it was added gets
/// one on a later refresh, and a series that has one is never asked about it again.
/// </summary>
// Built directly, like SeriesRefresherDescriptionTests, because the subject is one series'
// worth of work; SyncPipelineCoverTests drives the same heal through the app's wiring.
public sealed class SeriesRefresherCoverTests
{
    private const int AniListId = 85470;
    private const long MangaUpdatesId = 24348008396;
    private const string AniListCoverUrl = "https://s4.anilist.co/file/anilistcdn/media/manga/cover/large/85470.jpg";
    private const string MangaUpdatesCoverUrl = "https://cdn.mangaupdates.com/image/i123.jpg";
    private const string AniListKey = "anilist/85470.jpg";
    private const string MangaUpdatesKey = "mangaupdates/24348008396.jpg";

    [Fact]
    public async Task RefreshAsync_ACoverThatFailedToCacheBefore_IsCachedOnTheNextRefreshThatSucceeds()
    {
        var series = CreateSeries();
        var coverCache = Substitute.For<ISeriesCoverCache>();
        coverCache.EnsureCachedAsync(MetadataSourceName.AniList, AniListId, AniListCoverUrl, Arg.Any<CancellationToken>())
            .Returns((string?)null, AniListKey);
        var refresher = CreateRefresher(MetadataSource(), coverCache);

        await RefreshAsync(refresher, series);

        // The store or the CDN hiccuped again: still no key, so the next run asks again.
        series.CoverObjectKey.ShouldBeNull();

        await RefreshAsync(refresher, series);

        series.CoverObjectKey.ShouldBe(AniListKey);
    }

    [Fact]
    public async Task RefreshAsync_ASeriesThatHasACover_IsNeverAskedAboutIt()
    {
        var series = CreateSeries();
        series.Description = "Already written down.";
        series.CoverObjectKey = "anilist/85470.png";
        var metadataSource = MetadataSource();
        var coverCache = Substitute.For<ISeriesCoverCache>();

        await RefreshAsync(CreateRefresher(metadataSource, coverCache), series);

        // Self-extinguishing: once a key is written the guard stops firing, which is what makes
        // a heal with no budget affordable.
        await metadataSource.DidNotReceive().GetSeriesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        await coverCache.DidNotReceiveWithAnyArgs().EnsureCachedAsync(default, default, default, default);
        series.CoverObjectKey.ShouldBe("anilist/85470.png");
    }

    [Fact]
    public async Task RefreshAsync_NoDescriptionAndNoCover_AsksAniListOnceForBoth()
    {
        var series = CreateSeries();
        var metadataSource = MetadataSource();

        await RefreshAsync(CreateRefresher(metadataSource, CoverCache()), series);

        series.Description.ShouldBe("A synopsis.");
        series.CoverObjectKey.ShouldBe(AniListKey);
        await metadataSource.Received(1).GetSeriesAsync(AniListId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshAsync_AniListThrows_LeavesTheCoverNullWithoutAskingTwiceAndCarriesOn()
    {
        var series = CreateSeries();
        var metadataSource = Substitute.For<ISeriesMetadataSource>();
        metadataSource.GetSeriesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns<Task<SeriesMetadata?>>(_ => throw new HttpRequestException("AniList is down"));

        var run = await RefreshAsync(CreateRefresher(metadataSource, CoverCache()), series);

        // One failed request between the two heals, not one each, and not a failed refresh.
        await metadataSource.Received(1).GetSeriesAsync(AniListId, Arg.Any<CancellationToken>());
        series.CoverObjectKey.ShouldBeNull();
        run.VolumesMarkedReleased.ShouldBe(3);
        run.Failures.ShouldBe(0);
    }

    [Fact]
    public async Task RefreshAsync_ASeriesWithOnlyAMangaUpdatesId_IsHealedFromMangaUpdates()
    {
        // Added through the MangaUpdates fallback while AniList was down: no AniList id at all.
        var series = CreateSeries();
        series.AniListId = null;
        series.MangaUpdatesId = MangaUpdatesId;

        var mangaUpdates = Substitute.For<IMangaUpdatesSeriesQueryService>();
        mangaUpdates.GetSeriesAsync(MangaUpdatesId, Arg.Any<CancellationToken>())
            .Returns(new MangaUpdatesSeriesMetadata
            {
                SourceId = MangaUpdatesId,
                TitleEnglish = "Mushoku Tensei",
                Format = Format.LightNovel,
                PublicationStatus = PublicationStatus.Ongoing,
                CoverImageUrl = MangaUpdatesCoverUrl,
            });
        var metadataSource = MetadataSource();

        await RefreshAsync(CreateRefresher(metadataSource, CoverCache(), mangaUpdates), series, ReleaseInfo());

        series.CoverObjectKey.ShouldBe(MangaUpdatesKey);
        await metadataSource.DidNotReceive().GetSeriesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshAsync_AVolumeWithAUserCover_KeepsItWhileTheSeriesCoverIsHealed()
    {
        var series = CreateSeries();
        var volume = series.Volumes.First();
        volume.UserState = new VolumeUserState { CoverOverrideObjectKey = $"overrides/{volume.Id}/upload.webp" };

        await RefreshAsync(CreateRefresher(MetadataSource(), CoverCache()), series);

        // The heal writes the series' synced key and nothing else: an override is user data
        // (CLAUDE.md — Cover overrides), and still wins on every tile it sits on.
        series.CoverObjectKey.ShouldBe(AniListKey);
        volume.UserState.CoverOverrideObjectKey.ShouldBe($"overrides/{volume.Id}/upload.webp");
        SeriesDetailProjection.ToVolume(volume, DateOnly.MinValue, series.CoverObjectKey)
            .CoverObjectKey.ShouldBe($"overrides/{volume.Id}/upload.webp");
    }

    private static async Task<SyncRun> RefreshAsync(
        SeriesRefresher refresher,
        Series series,
        ReleaseSeriesInfo? releaseInfo = null)
    {
        var run = new SyncRun { Id = Guid.CreateVersion7(), StartedAt = DateTimeOffset.UnixEpoch };

        await refresher.RefreshAsync(series, run, [], new EnrichmentBudget(8, 8), CancellationToken.None);

        return run;
    }

    private static SeriesRefresher CreateRefresher(
        ISeriesMetadataSource metadataSource,
        ISeriesCoverCache coverCache,
        IMangaUpdatesSeriesQueryService? mangaUpdates = null)
    {
        var matcher = Substitute.For<IReleaseSourceMatcher>();
        matcher.MatchAsync(Arg.Any<Series>(), Arg.Any<IReadOnlySet<long>>(), Arg.Any<CancellationToken>())
            .Returns(new ReleaseSourceMatch
            {
                Outcome = ReleaseMatchOutcome.Linked,
                MatchedExternalId = MangaUpdatesId,
                Score = 100,
                Explanation = "Linked.",
                MatchedSeries = ReleaseInfo(),
            });

        var releaseSource = Substitute.For<IReleaseSource>();
        releaseSource.SourceName.Returns("MangaUpdates");
        releaseSource.GetSeriesAsync(MangaUpdatesId, Arg.Any<CancellationToken>()).Returns(ReleaseInfo());

        return new SeriesRefresher(
            matcher,
            releaseSource,
            metadataSource,
            new SeriesDescriptionHealer(NullLogger<SeriesDescriptionHealer>.Instance),
            new SeriesCoverHealer(
                mangaUpdates ?? Substitute.For<IMangaUpdatesSeriesQueryService>(),
                coverCache,
                NullLogger<SeriesCoverHealer>.Instance),
            new ReleaseCountWriter(releaseSource, NullLogger<ReleaseCountWriter>.Instance),
            new PublicationStatusWriter(releaseSource, NullLogger<PublicationStatusWriter>.Instance),
            VolumeEnrichmentPipeline.Create(
                NoCatalogue(),
                NoEnrichment(),
                NoCandidates(),
                VolumeEnrichmentPipeline.Writer()),
            ChapterMapPipeline.NoSources(),
            TimeProvider.System);
    }

    private static ISeriesMetadataSource MetadataSource()
    {
        var source = Substitute.For<ISeriesMetadataSource>();
        source.GetSeriesAsync(AniListId, Arg.Any<CancellationToken>())
            .Returns(new SeriesMetadata
            {
                ExternalId = AniListId,
                TitleEnglish = "Mushoku Tensei: Jobless Reincarnation",
                Format = Format.LightNovel,
                PublicationStatus = PublicationStatus.Completed,
                Description = "A synopsis.",
                CoverImageUrl = AniListCoverUrl,
            });

        return source;
    }

    /// <summary>A cover cache that answers with each source's key, as a working CDN and store do.</summary>
    private static ISeriesCoverCache CoverCache()
    {
        var coverCache = Substitute.For<ISeriesCoverCache>();
        coverCache.EnsureCachedAsync(MetadataSourceName.AniList, AniListId, AniListCoverUrl, Arg.Any<CancellationToken>())
            .Returns(AniListKey);
        coverCache.EnsureCachedAsync(MetadataSourceName.MangaUpdates, MangaUpdatesId, MangaUpdatesCoverUrl, Arg.Any<CancellationToken>())
            .Returns(MangaUpdatesKey);

        return coverCache;
    }

    private static ReleaseSeriesInfo ReleaseInfo() => new()
    {
        ExternalId = MangaUpdatesId,
        Title = "Mushoku Tensei",
        VolumeCount = 3,
        RawStatus = "3 Volumes (Ongoing)",
    };

    private static Series CreateSeries()
    {
        var series = new Series
        {
            Id = Guid.CreateVersion7(),
            AniListId = AniListId,
            TitleEnglish = "Mushoku Tensei: Jobless Reincarnation",
            Format = Format.LightNovel,
            PublicationStatus = PublicationStatus.Completed,
        };

        foreach (var number in Enumerable.Range(1, 3))
        {
            series.Volumes.Add(new Volume { Id = Guid.CreateVersion7(), Number = number });
        }

        return series;
    }
}
