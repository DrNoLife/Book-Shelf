using static LnShelf.Api.Tests.Features.Metadata.Services.VolumeEnrichmentTestContext;

namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// The description heal: a series added before the shelf asked AniList for a synopsis
/// gets one on its next refresh, and never asks twice.
/// </summary>
// The refresher is built directly rather than through the sync service, because what is
// under test is one series' worth of work and the run around it is not the point.
public sealed class SeriesRefresherDescriptionTests
{
    private const int AniListId = 85470;
    private const long MangaUpdatesId = 24348008396;

    private const string Description = "A 34-year-old NEET wakes as Rudeus Greyrat.";

    [Fact]
    public async Task RefreshAsync_ASeriesWithNoDescription_FillsItInFromAniList()
    {
        var series = CreateSeries();
        var metadataSource = MetadataSource(Description);

        await RefreshAsync(series, metadataSource);

        series.Description.ShouldBe(Description);
    }

    [Fact]
    public async Task RefreshAsync_ASeriesThatAlreadyHasOne_DoesNotAskAniListAgain()
    {
        var series = CreateSeries();
        series.Description = "Already written down.";

        var metadataSource = MetadataSource(Description);

        await RefreshAsync(series, metadataSource);

        // Self-extinguishing is the whole reason this needs no lookup budget: once the
        // column is filled the guard stops firing, so the cost is one request per series
        // ever rather than one per nightly run.
        await metadataSource.DidNotReceive().GetSeriesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        series.Description.ShouldBe("Already written down.");
    }

    [Fact]
    public async Task RefreshAsync_ASeriesWithNoAniListId_IsNotAsked()
    {
        // A manual WebNovel entry (feature-spec §4.2): there is no id to ask about.
        var series = CreateSeries();
        series.AniListId = null;

        var metadataSource = MetadataSource(Description);

        await RefreshAsync(series, metadataSource);

        await metadataSource.DidNotReceive().GetSeriesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        series.Description.ShouldBeNull();
    }

    [Fact]
    public async Task RefreshAsync_AniListHasNoSynopsisForIt_WritesTheEmptyAnswer()
    {
        var series = CreateSeries();

        await RefreshAsync(series, MetadataSource(description: null));

        // Nothing is invented, but the answer is kept: empty means "asked, AniList has none",
        // the same state Volume.Title uses, and it is what makes the heal self-extinguishing.
        series.Description.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task RefreshAsync_AniListAlreadySaidItHasNoSynopsis_DoesNotAskAgain()
    {
        var series = CreateSeries();
        var metadataSource = MetadataSource(description: null);

        await RefreshAsync(series, metadataSource);
        await RefreshAsync(series, metadataSource);

        // Once, ever — not once a night for a synopsis AniList has already said it lacks.
        await metadataSource.Received(1).GetSeriesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshAsync_AniListTimesOut_LeavesTheDescriptionNullAndCarriesOn()
    {
        var series = CreateSeries();

        // HttpClient.Timeout's shape: a cancelled task that nobody cancelled.
        var metadataSource = Substitute.For<ISeriesMetadataSource>();
        metadataSource.GetSeriesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns<Task<SeriesMetadata?>>(_ => throw new TaskCanceledException("The request timed out."));

        var run = await RefreshAsync(series, metadataSource);

        // Not an answer, so nothing is written and the next run asks again — and not a
        // reason to throw away the volume counts this refresh already applied.
        series.Description.ShouldBeNull();
        run.VolumesMarkedReleased.ShouldBe(3);
    }

    [Fact]
    public async Task RefreshAsync_AniListThrows_TheRestOfTheRefreshStillHappens()
    {
        var series = CreateSeries();

        var metadataSource = Substitute.For<ISeriesMetadataSource>();
        metadataSource.GetSeriesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns<Task<SeriesMetadata?>>(_ => throw new HttpRequestException("AniList is down"));

        var run = await RefreshAsync(series, metadataSource);

        // A synopsis is the least of what a refresh is for. The volume counts still landed
        // and the run recorded no failure, which is this file's failure philosophy applied
        // one level down.
        series.Description.ShouldBeNull();
        series.MangaUpdatesId.ShouldBe(MangaUpdatesId);
        run.VolumesMarkedReleased.ShouldBe(3);
        run.Failures.ShouldBe(0);
    }

    [Fact]
    public async Task RefreshAsync_AnUnmatchedSeries_StillGetsItsDescription()
    {
        var series = CreateSeries();
        var metadataSource = MetadataSource(Description);

        await RefreshAsync(series, metadataSource, match: new ReleaseSourceMatch
        {
            Outcome = ReleaseMatchOutcome.NoCandidate,
            Explanation = "Nothing found.",
        });

        // Whether MangaUpdates recognises a series has nothing to do with whether AniList
        // has a synopsis for it, so the heal does not sit behind the release-source result.
        series.Description.ShouldBe(Description);
    }

    private static async Task<SyncRun> RefreshAsync(
        Series series,
        ISeriesMetadataSource metadataSource,
        ReleaseSourceMatch? match = null)
    {
        var run = new SyncRun { Id = Guid.CreateVersion7(), StartedAt = DateTimeOffset.UnixEpoch };

        await CreateRefresher(metadataSource, match ?? Linked()).RefreshAsync(
            series, run, [], new EnrichmentBudget(8, 8), CancellationToken.None);

        return run;
    }

    private static SeriesRefresher CreateRefresher(
        ISeriesMetadataSource metadataSource,
        ReleaseSourceMatch match)
    {
        var matcher = Substitute.For<IReleaseSourceMatcher>();
        matcher.MatchAsync(Arg.Any<Series>(), Arg.Any<IReadOnlySet<long>>(), Arg.Any<CancellationToken>())
            .Returns(match);

        var releaseSource = Substitute.For<IReleaseSource>();
        releaseSource.SourceName.Returns("MangaUpdates");

        return new SeriesRefresher(
            matcher,
            releaseSource,
            new SeriesDescriptionHealer(metadataSource, NullLogger<SeriesDescriptionHealer>.Instance),
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

    private static ISeriesMetadataSource MetadataSource(string? description)
    {
        var source = Substitute.For<ISeriesMetadataSource>();
        source.GetSeriesAsync(AniListId, Arg.Any<CancellationToken>())
            .Returns(new SeriesMetadata
            {
                ExternalId = AniListId,
                TitleEnglish = "Mushoku Tensei: Jobless Reincarnation",
                Format = Format.LightNovel,
                PublicationStatus = PublicationStatus.Completed,
                Description = description,
            });

        return source;
    }

    private static ReleaseSourceMatch Linked() => new()
    {
        Outcome = ReleaseMatchOutcome.Linked,
        MatchedExternalId = MangaUpdatesId,
        Score = 100,
        Explanation = "Linked.",
        MatchedSeries = new ReleaseSeriesInfo
        {
            ExternalId = MangaUpdatesId,
            Title = "Mushoku Tensei",
            VolumeCount = 3,
            RawStatus = "3 Volumes (Ongoing)",
        },
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
