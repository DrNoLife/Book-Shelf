using LnShelf.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// Refreshing one series on demand — what runs the moment a series is added. The same
/// work as the nightly sweep, on one series, with a smaller lookup allowance.
/// </summary>
public sealed class SeriesSyncServiceOnDemandTests
{
    private const long MangaUpdatesId = 24348008396;

    [Fact]
    public async Task RunForSeriesAsync_AJustAddedSeries_LinksItAndMarksItsVolumesInPrint()
    {
        await using var dbContext = CreateDbContext();
        var seriesId = await SeedAsync(dbContext, volumeCount: 14);

        var run = await CreateService(dbContext).RunForSeriesAsync(seriesId, CancellationToken.None);

        // The half that needs no ISBN and is what makes "to buy" work at all. Without this,
        // a newly added series reports published = 0 while tracking 14 volumes.
        run.ShouldNotBeNull().SeriesLinked.ShouldBe(1);
        run.VolumesMarkedReleased.ShouldBe(14);
        run.SeriesRefreshed.ShouldBe(1);

        (await dbContext.Series.SingleAsync()).MangaUpdatesId.ShouldBe(MangaUpdatesId);
    }

    [Fact]
    public async Task RunForSeriesAsync_TheRunItWrites_NamesTheSeriesRatherThanACount()
    {
        await using var dbContext = CreateDbContext();
        var seriesId = await SeedAsync(dbContext, volumeCount: 3);

        var run = await CreateService(dbContext).RunForSeriesAsync(seriesId, CancellationToken.None);

        // This run becomes the latest, and the sidebar prints it. "1 series refreshed" would
        // be true and useless; the title says which one and why it happened.
        run.ShouldNotBeNull().Summary.ShouldStartWith("The Saga of Tanya the Evil refreshed");

        // Still honest about the release model, exactly as the sweep's line is.
        run.Summary.ShouldContain("counts only");
    }

    [Fact]
    public async Task RunForSeriesAsync_OnlyTheSeriesAskedFor_IsTouched()
    {
        await using var dbContext = CreateDbContext();
        var wanted = await SeedAsync(dbContext, volumeCount: 3);
        await SeedAsync(dbContext, volumeCount: 3, titleEnglish: "Overlord", aniListId: 85976);

        await CreateService(dbContext).RunForSeriesAsync(wanted, CancellationToken.None);

        // The other series is left entirely alone — an add must not quietly re-sync the shelf.
        var other = await dbContext.Series.SingleAsync(series => series.TitleEnglish == "Overlord");
        other.MangaUpdatesId.ShouldBeNull();
        other.LastSyncedAt.ShouldBeNull();
    }

    [Fact]
    public async Task RunForSeriesAsync_ASeriesWhoseIdIsUnknown_IsNullAndWritesNoRun()
    {
        await using var dbContext = CreateDbContext();

        (await CreateService(dbContext).RunForSeriesAsync(Guid.CreateVersion7(), CancellationToken.None))
            .ShouldBeNull();

        // No run row for a series that does not exist: the sidebar would otherwise show a
        // refresh of nothing as the latest thing that happened.
        (await dbContext.SyncRuns.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task RunForSeriesAsync_AnIdAnotherSeriesHasAlreadyClaimed_IsNotStolen()
    {
        await using var dbContext = CreateDbContext();

        // Tanya is already linked to the record; the new series must not be given it too.
        var linked = await SeedAsync(dbContext, volumeCount: 3);
        (await dbContext.Series.SingleAsync(series => series.Id == linked)).MangaUpdatesId = MangaUpdatesId;
        await dbContext.SaveChangesAsync();

        var newcomer = await SeedAsync(dbContext, volumeCount: 3, titleEnglish: "Youjo Senki", aniListId: 111);

        var run = await CreateService(dbContext, alreadyLinked: true)
            .RunForSeriesAsync(newcomer, CancellationToken.None);

        // Recorded for a person rather than resolved by guessing, the same as in a sweep.
        var discrepancy = run.ShouldNotBeNull().Discrepancies.ShouldHaveSingleItem();
        discrepancy.Field.ShouldBe(SyncDiscrepancyField.SeriesLink);
        discrepancy.ResolvedWith.ShouldBeNull();

        (await dbContext.Series.SingleAsync(series => series.Id == newcomer)).MangaUpdatesId.ShouldBeNull();
    }

    [Fact]
    public async Task RunForSeriesAsync_ItsLookupAllowance_IsTheOnDemandOneNotTheNightlyOne()
    {
        await using var dbContext = CreateDbContext();
        var seriesId = await SeedAsync(dbContext, volumeCount: 20, withIsbns: true);

        var enrichmentSource = Substitute.For<IVolumeEnrichmentSource>();
        enrichmentSource.SourceName.Returns("OpenLibrary");
        enrichmentSource.GetByIsbnAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((VolumeEnrichment?)null);

        await CreateService(dbContext, enrichmentSource: enrichmentSource)
            .RunForSeriesAsync(seriesId, CancellationToken.None);

        // Somebody is waiting for this one, so it takes a small bite and the nightly sweep
        // finishes the rest — 20 volumes' worth of requests at twelve a minute is not a click.
        //
        // Four volumes, not eight, because the budget counts *requests* rather than volumes:
        // every one of these ISBNs comes back unindexed, so each volume costs a direct lookup
        // plus a fallback search. Counting a volume as one lookup would let a series quietly
        // spend several times its share against somebody else's donated API.
        await enrichmentSource.Received(OnDemandBudget / 2)
            .GetByIsbnAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private const int OnDemandBudget = 8;

    private static SeriesSyncService CreateService(
        LnShelfDbContext dbContext,
        bool alreadyLinked = false,
        IVolumeEnrichmentSource? enrichmentSource = null)
    {
        var matcher = Substitute.For<IReleaseSourceMatcher>();
        matcher.MatchAsync(Arg.Any<Series>(), Arg.Any<IReadOnlySet<long>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => alreadyLinked
                ? new ReleaseSourceMatch
                {
                    Outcome = ReleaseMatchOutcome.AlreadyLinked,
                    Score = 96,
                    Explanation = "Already linked to another series.",
                }
                : new ReleaseSourceMatch
                {
                    Outcome = ReleaseMatchOutcome.Linked,
                    MatchedExternalId = MangaUpdatesId,
                    Score = 100,
                    Explanation = "Linked.",
                    MatchedSeries = new ReleaseSeriesInfo
                    {
                        ExternalId = MangaUpdatesId,
                        Title = "Youjo Senki",
                        VolumeCount = callInfo.Arg<Series>().Volumes.Count,
                        RawStatus = "Ongoing",
                    },
                });

        var releaseSource = Substitute.For<IReleaseSource>();
        releaseSource.SourceName.Returns("MangaUpdates");
        releaseSource.GetSeriesAsync(MangaUpdatesId, Arg.Any<CancellationToken>())
            .Returns(new ReleaseSeriesInfo
            {
                ExternalId = MangaUpdatesId,
                Title = "Youjo Senki",
                VolumeCount = 3,
                RawStatus = "3 Volumes (Ongoing)",
            });

        enrichmentSource ??= Substitute.For<IVolumeEnrichmentSource>();
        enrichmentSource.SourceName.Returns("OpenLibrary");

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
            Options.Create(new OpenLibraryOptions
            {
                MaximumLookupsPerRun = 60,
                MaximumLookupsOnDemand = OnDemandBudget,
            }),
            TimeProvider.System,
            NullLogger<SeriesSyncService>.Instance);
    }

    private static async Task<Guid> SeedAsync(
        LnShelfDbContext dbContext,
        int volumeCount,
        string titleEnglish = "The Saga of Tanya the Evil",
        int aniListId = 94846,
        bool withIsbns = false)
    {
        var series = new Series
        {
            Id = Guid.CreateVersion7(),
            AniListId = aniListId,
            TitleEnglish = titleEnglish,
            TitleRomaji = "Youjo Senki",
            Format = Format.LightNovel,
            PublicationStatus = PublicationStatus.Ongoing,
        };

        foreach (var number in Enumerable.Range(1, volumeCount))
        {
            series.Volumes.Add(new Volume
            {
                Id = Guid.CreateVersion7(),
                Number = number,

                // Distinct, English-market, and valid — the enrichment source is only
                // reached for a volume whose ISBN passes both those checks.
                Isbn = withIsbns ? $"978031636{number:D4}" : null,
            });
        }

        dbContext.Series.Add(series);
        await dbContext.SaveChangesAsync();

        return series.Id;
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
            .UseInMemoryDatabase($"on-demand-{Guid.CreateVersion7()}")
            .Options);
}
