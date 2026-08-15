using LnShelf.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// The sync run: what it links, what it records, what it must never touch
/// (testing.md #3 — the bugs that corrupt data quietly).
/// </summary>
public sealed class SeriesSyncServiceTests
{
    private const long MangaUpdatesId = 24348008396;

    [Fact]
    public async Task RunAsync_AnUnlinkedSeries_LinksItAndRecordsNothingToReview()
    {
        await using var dbContext = CreateDbContext();
        await SeedSeriesAsync(dbContext, volumeCount: 14);

        var run = await CreateService(dbContext, Linked(volumeCount: 14)).RunAsync(CancellationToken.None);

        run.SeriesRefreshed.ShouldBe(1);
        run.SeriesLinked.ShouldBe(1);
        run.Failures.ShouldBe(0);
        run.Discrepancies.ShouldBeEmpty();

        var series = await dbContext.Series.SingleAsync();
        series.MangaUpdatesId.ShouldBe(MangaUpdatesId);
        series.LastSyncedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task RunAsync_AnUnmatchedSeries_RecordsItForReviewRatherThanGuessing()
    {
        await using var dbContext = CreateDbContext();
        await SeedSeriesAsync(dbContext, volumeCount: 3);

        var match = new ReleaseSourceMatch
        {
            Outcome = ReleaseMatchOutcome.AlreadyLinked,
            Score = 96,
            Explanation = "'Ascendance of a Bookworm' (37107680441) is already linked to another series.",
        };

        var run = await CreateService(dbContext, match).RunAsync(CancellationToken.None);

        run.SeriesLinked.ShouldBe(0);

        var discrepancy = run.Discrepancies.ShouldHaveSingleItem();
        discrepancy.Field.ShouldBe(SyncDiscrepancyField.SeriesLink);

        // Unresolved on purpose: a person has to decide, and null is how the review
        // list knows to ask.
        discrepancy.ResolvedWith.ShouldBeNull();
        discrepancy.Note.ShouldNotBeNull().ShouldContain("AlreadyLinked");

        (await dbContext.Series.SingleAsync()).MangaUpdatesId.ShouldBeNull();
    }

    [Fact]
    public async Task RunAsync_SourcesDisagreeOnVolumeCount_RecordsItAndKeepsAniList()
    {
        await using var dbContext = CreateDbContext();
        await SeedSeriesAsync(dbContext, volumeCount: 18);

        // The real Overlord case: AniList says 18, MangaUpdates says 16.
        var run = await CreateService(dbContext, Linked(volumeCount: 16)).RunAsync(CancellationToken.None);

        var discrepancy = run.Discrepancies.ShouldHaveSingleItem();
        discrepancy.Field.ShouldBe(SyncDiscrepancyField.VolumeCount);

        // Recorded as (source, value) pairs, which is what lets a third source disagree
        // at all — the shape used to be one named column per source, and there were two.
        discrepancy.Values.Single(value => value.Source is MetadataSourceName.AniList)
            .Value.ShouldBe("18");
        discrepancy.Values.Single(value => value.Source is MetadataSourceName.MangaUpdates)
            .Value.ShouldBe("16");

        // The conflict rule: MangaUpdates wins on dates, AniList on everything else.
        discrepancy.ResolvedWith.ShouldBe(MetadataSourceName.AniList);

        // Recorded, not acted on — nothing was deleted.
        (await dbContext.Volumes.CountAsync()).ShouldBe(18);
    }

    [Fact]
    public async Task RunAsync_TheSourceKnowsMoreVolumes_CreatesTheMissingOnesWithoutDates()
    {
        await using var dbContext = CreateDbContext();
        await SeedSeriesAsync(dbContext, volumeCount: 11);

        // The real 86 case: the shelf has 11 from the import, MangaUpdates says 14.
        var run = await CreateService(dbContext, Linked(volumeCount: 14)).RunAsync(CancellationToken.None);

        run.NewVolumes.ShouldBe(3);

        var volumes = await dbContext.Volumes.OrderBy(volume => volume.Number).ToListAsync();
        volumes.Count.ShouldBe(14);
        volumes.Select(volume => volume.Number).ShouldBe(Enumerable.Range(1, 14));

        // That they exist is a fact from the release source; when they came out is
        // not something it knows, so no date is invented.
        volumes.TakeLast(3).ShouldAllBe(volume => volume.ReleaseDate == null);
        volumes.TakeLast(3).ShouldAllBe(volume => !volume.IsLocalOnly);

        // And the run reports no dates moved, because none can be.
        run.DatesMoved.ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_EverySeries_KeepsItsUserStateUntouched()
    {
        await using var dbContext = CreateDbContext();
        await SeedSeriesAsync(dbContext, volumeCount: 3, withUserState: true);

        await CreateService(dbContext, Linked(volumeCount: 9)).RunAsync(CancellationToken.None);

        var series = await dbContext.Series
            .Include(candidate => candidate.UserState)
            .Include(candidate => candidate.Volumes).ThenInclude(volume => volume.UserState)
            .SingleAsync();

        // Sync never writes user state (CLAUDE.md). Six new volumes arrived and not
        // one of the three existing states changed.
        series.UserState.ShouldNotBeNull().TrackingStatus.ShouldBe(TrackingStatus.Reading);
        series.Volumes.Where(volume => volume.Number <= 3)
            .ShouldAllBe(volume => volume.UserState!.ReadStatus == ReadStatus.Read && volume.UserState.Owned);

        // The volumes it created have no user state at all — absence is Unread and
        // NotOwned, which is the truth about a volume nobody has touched.
        series.Volumes.Where(volume => volume.Number > 3)
            .ShouldAllBe(volume => volume.UserState == null);
    }

    [Fact]
    public async Task RunAsync_OneSeriesThrows_TheRestStillRefresh()
    {
        await using var dbContext = CreateDbContext();
        await SeedSeriesAsync(dbContext, volumeCount: 3, titleEnglish: "Explodes", aniListId: 1);
        await SeedSeriesAsync(dbContext, volumeCount: 3, titleEnglish: "Survives", aniListId: 2);

        var matcher = Substitute.For<IReleaseSourceMatcher>();
        matcher.MatchAsync(
                Arg.Is<Series>(series => series.TitleEnglish == "Explodes"),
                Arg.Any<IReadOnlySet<long>>(),
                Arg.Any<CancellationToken>())
            .Returns<Task<ReleaseSourceMatch>>(_ => throw new HttpRequestException("MangaUpdates is down"));
        matcher.MatchAsync(
                Arg.Is<Series>(series => series.TitleEnglish == "Survives"),
                Arg.Any<IReadOnlySet<long>>(),
                Arg.Any<CancellationToken>())
            .Returns(Linked(volumeCount: 3));

        var run = await CreateService(dbContext, matcher).RunAsync(CancellationToken.None);

        // Per-series failure isolation: one failure logs and the run continues.
        run.Failures.ShouldBe(1);
        run.SeriesRefreshed.ShouldBe(1);
        run.SeriesLinked.ShouldBe(1);
        run.Summary.ShouldContain("1 failed");

        (await dbContext.Series.SingleAsync(series => series.TitleEnglish == "Survives"))
            .MangaUpdatesId.ShouldBe(MangaUpdatesId);
    }

    [Fact]
    public async Task RunAsync_ASeriesWithNoAniListId_IsSkipped()
    {
        await using var dbContext = CreateDbContext();

        // A manual WebNovel entry: nothing to refresh against (feature-spec §4.2).
        dbContext.Series.Add(new Series
        {
            Id = Guid.CreateVersion7(),
            AniListId = null,
            TitleEnglish = "Omniscient Reader's Viewpoint",
            Format = Format.WebNovel,
            PublicationStatus = PublicationStatus.Ongoing,
        });
        await dbContext.SaveChangesAsync();

        var matcher = Substitute.For<IReleaseSourceMatcher>();
        var run = await CreateService(dbContext, matcher).RunAsync(CancellationToken.None);

        run.SeriesRefreshed.ShouldBe(0);
        await matcher.DidNotReceive().MatchAsync(
            Arg.Any<Series>(), Arg.Any<IReadOnlySet<long>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_TwiceOverTheSameShelf_ChangesNothingTheSecondTime()
    {
        await using var dbContext = CreateDbContext();
        await SeedSeriesAsync(dbContext, volumeCount: 14);

        await CreateService(dbContext, Linked(volumeCount: 14)).RunAsync(CancellationToken.None);
        var second = await CreateService(dbContext, Linked(volumeCount: 14)).RunAsync(CancellationToken.None);

        // Already linked, so nothing to link; counts agree, so nothing to record.
        second.SeriesLinked.ShouldBe(0);
        second.NewVolumes.ShouldBe(0);
        second.Discrepancies.ShouldBeEmpty();
        (await dbContext.Volumes.CountAsync()).ShouldBe(14);
    }

    [Fact]
    public async Task RunAsync_Always_SaysWhereTheReleaseModelStops()
    {
        await using var dbContext = CreateDbContext();
        await SeedSeriesAsync(dbContext, volumeCount: 3);

        var run = await CreateService(dbContext, Linked(volumeCount: 3)).RunAsync(CancellationToken.None);

        // Transparent, not magical: the release source's word is a count, dates reach only
        // the volumes whose ISBN is known, and the footer must not imply more than that.
        run.Summary.ShouldContain("counts only");
        run.Summary.ShouldContain("dates where an ISBN is known");

        // These volumes have no ISBN, so nothing was enriched and no date could arrive.
        run.DatesMoved.ShouldBe(0);
        run.VolumesEnriched.ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_TheSourcesCount_MarksThoseVolumesInPrint()
    {
        await using var dbContext = CreateDbContext();
        await SeedSeriesAsync(dbContext, volumeCount: 14);

        var run = await CreateService(dbContext, Linked(volumeCount: 14)).RunAsync(CancellationToken.None);

        run.VolumesMarkedReleased.ShouldBe(14);

        var volumes = await dbContext.Volumes.ToListAsync();
        volumes.ShouldAllBe(volume => volume.IsReleased);

        // Still no dates. "In print" is what the source says; when is not.
        volumes.ShouldAllBe(volume => volume.ReleaseDate == null);
    }

    [Fact]
    public async Task RunAsync_VolumesBeyondTheSourcesCount_AreLeftUnreleased()
    {
        await using var dbContext = CreateDbContext();

        // The real Overlord shape: AniList tracks 18, MangaUpdates counts 16.
        await SeedSeriesAsync(dbContext, volumeCount: 18);

        var run = await CreateService(dbContext, Linked(volumeCount: 16)).RunAsync(CancellationToken.None);

        run.VolumesMarkedReleased.ShouldBe(16);

        var volumes = await dbContext.Volumes.OrderBy(volume => volume.Number).ToListAsync();
        volumes.Where(volume => volume.Number <= 16).ShouldAllBe(volume => volume.IsReleased);
        volumes.Where(volume => volume.Number > 16).ShouldAllBe(volume => !volume.IsReleased);
    }

    [Fact]
    public async Task RunAsync_Twice_MarksNothingInPrintTheSecondTime()
    {
        await using var dbContext = CreateDbContext();
        await SeedSeriesAsync(dbContext, volumeCount: 14);

        await CreateService(dbContext, Linked(volumeCount: 14)).RunAsync(CancellationToken.None);
        var second = await CreateService(dbContext, Linked(volumeCount: 14)).RunAsync(CancellationToken.None);

        second.VolumesMarkedReleased.ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_AnUnmatchedSeries_HasNoVolumesMarkedInPrint()
    {
        await using var dbContext = CreateDbContext();
        await SeedSeriesAsync(dbContext, volumeCount: 3);

        var run = await CreateService(dbContext, new ReleaseSourceMatch
        {
            Outcome = ReleaseMatchOutcome.NoCandidate,
            Explanation = "Nothing found.",
        }).RunAsync(CancellationToken.None);

        // No link means no count means no claim. An unmatched series says nothing
        // about what is in print rather than guessing.
        run.VolumesMarkedReleased.ShouldBe(0);
        (await dbContext.Volumes.ToListAsync()).ShouldAllBe(volume => !volume.IsReleased);
    }

    private static ReleaseSourceMatch Linked(int? volumeCount) => new()
    {
        Outcome = ReleaseMatchOutcome.Linked,
        MatchedExternalId = MangaUpdatesId,
        Score = 100,
        Explanation = "Linked.",
        MatchedSeries = new ReleaseSeriesInfo
        {
            ExternalId = MangaUpdatesId,
            Title = "Youjo Senki",
            VolumeCount = volumeCount,
            RawStatus = $"{volumeCount} Volumes (Ongoing)",
            IsCompleted = false,
        },
    };

    private static SeriesSyncService CreateService(LnShelfDbContext dbContext, ReleaseSourceMatch match)
    {
        var matcher = Substitute.For<IReleaseSourceMatcher>();
        matcher.MatchAsync(Arg.Any<Series>(), Arg.Any<IReadOnlySet<long>>(), Arg.Any<CancellationToken>())
            .Returns(match);

        return CreateService(dbContext, matcher);
    }

    private static SeriesSyncService CreateService(
        LnShelfDbContext dbContext,
        IReleaseSourceMatcher matcher,
        IVolumeEnrichmentSource? enrichmentSource = null)
    {
        var releaseSource = Substitute.For<IReleaseSource>();
        releaseSource.SourceName.Returns("MangaUpdates");

        // An already-linked series is re-fetched rather than re-matched.
        releaseSource.GetSeriesAsync(MangaUpdatesId, Arg.Any<CancellationToken>())
            .Returns(callInfo => new ReleaseSeriesInfo
            {
                ExternalId = MangaUpdatesId,
                Title = "Youjo Senki",
                VolumeCount = 14,
                RawStatus = "14 Volumes (Ongoing)",
            });

        // Knows nothing by default: these tests are about the release-source half of the
        // run, and the enrichment half has its own file.
        enrichmentSource ??= EnrichmentSource();

        var enrichmentOptions = Options.Create(new OpenLibraryOptions { MaximumLookupsPerRun = 60 });

        var refresher = new SeriesRefresher(
            matcher,
            releaseSource,
            NoMetadata(),
            new VolumeEnrichmentService(
                enrichmentSource,
                NoCandidates(),
                Substitute.For<IVolumeCoverCache>(),
                NullLogger<VolumeEnrichmentService>.Instance),
            TimeProvider.System,
            NullLogger<SeriesRefresher>.Instance);

        return new SeriesSyncService(
            dbContext,
            refresher,
            releaseSource,
            enrichmentSource,
            enrichmentOptions,
            TimeProvider.System,
            NullLogger<SeriesSyncService>.Instance);
    }

    private static IVolumeEnrichmentSource EnrichmentSource(VolumeEnrichment? enrichment = null)
    {
        var source = Substitute.For<IVolumeEnrichmentSource>();
        source.SourceName.Returns("OpenLibrary");
        source.GetByIsbnAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(enrichment);

        return source;
    }

    private static async Task SeedSeriesAsync(
        LnShelfDbContext dbContext,
        int volumeCount,
        bool withUserState = false,
        string titleEnglish = "The Saga of Tanya the Evil",
        int aniListId = 94846)
    {
        var series = new Series
        {
            Id = Guid.CreateVersion7(),
            AniListId = aniListId,
            TitleEnglish = titleEnglish,
            TitleRomaji = "Youjo Senki",
            Format = Format.LightNovel,
            PublicationStatus = PublicationStatus.Ongoing,
            UserState = withUserState
                ? new SeriesUserState { TrackingStatus = TrackingStatus.Reading }
                : null,
        };

        foreach (var number in Enumerable.Range(1, volumeCount))
        {
            series.Volumes.Add(new Volume
            {
                Id = Guid.CreateVersion7(),
                Number = number,
                IsLocalOnly = true,
                UserState = withUserState
                    ? new VolumeUserState { ReadStatus = ReadStatus.Read, Owned = true }
                    : null,
            });
        }

        dbContext.Series.Add(series);
        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// AniList knows nothing: these tests are about the release-source half of a run, and
    /// the description heal has its own file.
    /// </summary>
    private static ISeriesMetadataSource NoMetadata()
    {
        var source = Substitute.For<ISeriesMetadataSource>();
        source.GetSeriesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((SeriesMetadata?)null);

        return source;
    }

    /// <summary>
    /// A search that offers nothing: these tests are about the release-source half of a
    /// run, and the search fallback has its own file.
    /// </summary>
    private static IIsbnCandidateSource NoCandidates()
    {
        var source = Substitute.For<IIsbnCandidateSource>();
        source.SourceName.Returns("OpenLibrary");
        source.FindAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(IsbnCandidates.None);

        return source;
    }

    private static LnShelfDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<LnShelfDbContext>()
            .UseInMemoryDatabase($"sync-{Guid.CreateVersion7()}")
            .Options);
}
