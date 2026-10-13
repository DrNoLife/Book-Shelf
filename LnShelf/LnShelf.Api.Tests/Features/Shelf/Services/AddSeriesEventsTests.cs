using LnShelf.Api.Tests.Features.Metadata.Services;
using Microsoft.EntityFrameworkCore;
using static LnShelf.Api.Tests.Features.Metadata.Services.VolumeEnrichmentTestContext;

namespace LnShelf.Api.Tests.Features.Shelf.Services;

/// <summary>
/// Adding a series, through the refresh that runs the moment it is added: its backlist
/// arriving is the shelf catching up, never news.
/// </summary>
// End to end through the pipeline and the real sync, because the bug this guards lived in the
// seam between them: the add stamped LastSyncedAt, so the add-time refresh read as a second
// one and logged every volume the release source counted as "out today".
public sealed class AddSeriesEventsTests
{
    private const int AniListId = 85470;
    private const long MangaUpdatesId = 24348008396;
    private const string Title = "Mushoku Tensei: Jobless Reincarnation";

    [Fact]
    public async Task RunAsync_AnAniListSeriesTheReleaseSourceCounts_LogsNoEventsAndRaisesNoNewBadge()
    {
        await using var dbContext = CreateDbContext();

        var response = await CreatePipeline(dbContext, releaseSourceCount: 14).RunAsync(
            new AddSeriesRequest { AniListId = AniListId, Format = Format.LightNovel },
            CancellationToken.None);

        response.ShouldNotBeNull().AlreadyTracked.ShouldBeFalse();

        var series = await dbContext.Series
            .AsNoTracking()
            .Include(candidate => candidate.UserState)
            .Include(candidate => candidate.Volumes)
            .SingleAsync();

        // The refresh did run and did mark the backlist out: that is what makes this a real test.
        series.LastSyncedAt.ShouldNotBeNull();
        series.Volumes.Count(volume => volume.IsReleased).ShouldBe(14);

        (await dbContext.ShelfEvents.CountAsync()).ShouldBe(0);
        series.UserState.ShouldNotBeNull().HasNewVolume.ShouldBeFalse();
    }

    private static AddSeriesPipeline CreatePipeline(LnShelfDbContext dbContext, int releaseSourceCount)
    {
        var metadataSource = Substitute.For<ISeriesMetadataSource>();
        metadataSource.GetSeriesAsync(AniListId, Arg.Any<CancellationToken>())
            .Returns(new SeriesMetadata
            {
                ExternalId = AniListId,
                TitleEnglish = Title,
                Format = Format.LightNovel,
                PublicationStatus = PublicationStatus.Completed,
                Description = "A synopsis, so the refresh asks AniList for nothing.",
                Volumes = [.. Enumerable.Range(1, releaseSourceCount).Select(number => new VolumeMetadata { Number = number })],
            });

        var releaseSource = Substitute.For<IReleaseSource>();
        releaseSource.SourceName.Returns("MangaUpdates");
        releaseSource.GetSeriesAsync(MangaUpdatesId, Arg.Any<CancellationToken>())
            .Returns(new ReleaseSeriesInfo
            {
                ExternalId = MangaUpdatesId,
                Title = Title,
                VolumeCount = releaseSourceCount,
                RawStatus = $"{releaseSourceCount} Volumes (Complete)",
            });

        // Linked on the way in but with no count, so the AniList volumes stand unreleased and
        // the refresh is what marks them out — the exact shape that used to flood the log.
        var matcher = Substitute.For<IReleaseSourceMatcher>();
        matcher.MatchAsync(Arg.Any<Series>(), Arg.Any<IReadOnlySet<long>>(), Arg.Any<CancellationToken>())
            .Returns(new ReleaseSourceMatch
            {
                Outcome = ReleaseMatchOutcome.Linked,
                MatchedExternalId = MangaUpdatesId,
                Explanation = "Linked in this test.",
            });

        return new AddSeriesPipeline(
            dbContext,
            [
                new AniListAddSeriesFlow(
                    metadataSource,
                    Substitute.For<ISeriesCoverCache>(),
                    new AddTimeReleaseLink(matcher, dbContext, NullLogger<AddTimeReleaseLink>.Instance)),
            ],
            SeriesCommandServiceTests.NoStagedIsbns(),
            CreateSync(dbContext, metadataSource, releaseSource, matcher),
            TestUsers.Limits(dbContext),
            TimeProvider.System,
            NullLogger<AddSeriesPipeline>.Instance);
    }

    private static SeriesSyncService CreateSync(
        LnShelfDbContext dbContext,
        ISeriesMetadataSource metadataSource,
        IReleaseSource releaseSource,
        IReleaseSourceMatcher matcher)
    {
        var enrichmentSource = NoEnrichment();
        enrichmentSource.SourceName.Returns("OpenLibrary");

        var refresher = new SeriesRefresher(
            matcher,
            releaseSource,
            metadataSource,
            new ReleaseCountWriter(releaseSource, NullLogger<ReleaseCountWriter>.Instance),
            new PublicationStatusWriter(releaseSource, NullLogger<PublicationStatusWriter>.Instance),
            VolumeEnrichmentPipeline.Create(NoCatalogue(), enrichmentSource, NoCandidates(), VolumeEnrichmentPipeline.Writer()),
            TimeProvider.System,
            NullLogger<SeriesRefresher>.Instance);

        return new SeriesSyncService(
            dbContext,
            refresher,
            releaseSource,
            NoCatalogue(),
            enrichmentSource,
            Options.Create(new OpenLibraryOptions { MaximumLookupsPerRun = 60, MaximumLookupsOnDemand = 6 }),
            TimeProvider.System,
            NullLogger<SeriesSyncService>.Instance);
    }

    private static LnShelfDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<LnShelfDbContext>()
            .UseInMemoryDatabase($"add-series-events-{Guid.CreateVersion7()}")
            .Options, TestUsers.Current);
}
