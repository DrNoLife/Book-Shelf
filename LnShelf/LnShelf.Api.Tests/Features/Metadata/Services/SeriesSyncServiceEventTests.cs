using Microsoft.EntityFrameworkCore;
using static LnShelf.Api.Tests.Features.Metadata.Services.VolumeEnrichmentTestContext;

namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// The activity log a sweep writes: saved with the series it is about, never for a first
/// refresh, and never twice for the same news.
/// </summary>
public sealed class SeriesSyncServiceEventTests
{
    private const long MangaUpdatesId = 24348008396;

    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Now);

    [Fact]
    public async Task RunAsync_TheSourceCountsMoreThanLastNight_LogsTheNewVolumes()
    {
        await using var dbContext = CreateDbContext();
        await SeedAsync(dbContext, volumeCount: 14, synced: true);

        await CreateService(dbContext, sourceCount: 16).RunAsync(CancellationToken.None);

        var events = await dbContext.ShelfEvents.OrderBy(shelfEvent => shelfEvent.VolumeNumber).ToListAsync();
        events.Select(shelfEvent => shelfEvent.VolumeNumber).ShouldBe([15m, 16m]);
        events.ShouldAllBe(shelfEvent => shelfEvent.Kind == ShelfEventKind.VolumeReleased && shelfEvent.NotifiedAt == null);
    }

    [Fact]
    public async Task RunAsync_ASeriesFirstRefresh_LogsNothing()
    {
        await using var dbContext = CreateDbContext();
        await SeedAsync(dbContext, volumeCount: 3, synced: false);

        await CreateService(dbContext, sourceCount: 16).RunAsync(CancellationToken.None);

        // Its whole backlist arriving at once is the shelf catching up, not news.
        (await dbContext.ShelfEvents.CountAsync()).ShouldBe(0);
    }

    // A record that came back empty leaves LastSyncedAt where it was, so the diff compares with
    // the same old night again; the log is what stops the date passing from being news twice.
    [Fact]
    public async Task RunAsync_TwiceWhileTheSourceHasNoRecord_LogsADatePassingOnce()
    {
        await using var dbContext = CreateDbContext();
        var seriesId = await SeedAsync(dbContext, volumeCount: 2, synced: true);
        var volume = await dbContext.Volumes.SingleAsync(candidate => candidate.SeriesId == seriesId && candidate.Number == 2);
        volume.IsReleased = false;
        volume.ReleaseDate = Today.AddDays(-1);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext, sourceCount: null);
        await service.RunAsync(CancellationToken.None);
        await service.RunAsync(CancellationToken.None);

        var shelfEvent = (await dbContext.ShelfEvents.ToListAsync()).ShouldHaveSingleItem();
        shelfEvent.Kind.ShouldBe(ShelfEventKind.VolumeReleased);
        shelfEvent.VolumeNumber.ShouldBe(2);
    }

    [Fact]
    public async Task RunAsync_TheSameCountAgain_LogsNothing()
    {
        // Idempotency (testing.md #3).
        await using var dbContext = CreateDbContext();
        await SeedAsync(dbContext, volumeCount: 14, synced: true);

        await CreateService(dbContext, sourceCount: 14).RunAsync(CancellationToken.None);

        (await dbContext.ShelfEvents.CountAsync()).ShouldBe(0);
    }

    // A person re-points a series by hand while a sweep that already loaded it is running. The
    // sweep must refresh it against the new record as a first refresh, not against the old one,
    // or it stamps LastSyncedAt over the link's null and the next night announces the new
    // record's whole backlist.
    [Fact]
    public async Task RunAsync_ALinkChangedByHandDuringTheSweep_LogsNothingThatNightOrTheNext()
    {
        const long OtherRecord = 11111111111;
        const long NewRecord = 22222222222;
        var databaseName = $"sync-events-{Guid.CreateVersion7()}";

        await using (var seeding = CreateDbContext(databaseName))
        {
            await SeedAsync(seeding, volumeCount: 3, synced: true);
            var first = await seeding.Series.SingleAsync();
            first.TitleEnglish = "A series swept first";
            first.MangaUpdatesId = OtherRecord;
            await SeedAsync(seeding, volumeCount: 3, synced: true);
        }

        Guid repointedId;
        await using (var reading = CreateDbContext(databaseName))
        {
            repointedId = await reading.Series
                .Where(candidate => candidate.MangaUpdatesId == MangaUpdatesId)
                .Select(candidate => candidate.Id)
                .SingleAsync();
        }

        var releaseSource = ReleaseSource((MangaUpdatesId, 3), (OtherRecord, 3), (NewRecord, 10));

        // While the sweep refreshes the first series, the link lands on the second — the way
        // ReleaseLinkCommandService saves it when the gate is busy. Once, on the first night.
        var linked = false;
        releaseSource.When(source => source.GetSeriesAsync(OtherRecord, Arg.Any<CancellationToken>()))
            .Do(_ =>
            {
                if (linked)
                {
                    return;
                }

                linked = true;
                using var request = CreateDbContext(databaseName);
                var repointed = request.Series.Single(candidate => candidate.Id == repointedId);
                repointed.MangaUpdatesId = NewRecord;
                repointed.LastSyncedAt = null;
                request.SaveChanges();
            });

        await using (var sweeping = CreateDbContext(databaseName))
        {
            await CreateService(sweeping, releaseSource).RunAsync(CancellationToken.None);
        }

        await using var nextNight = CreateDbContext(databaseName);
        await CreateService(nextNight, releaseSource).RunAsync(CancellationToken.None);

        (await nextNight.ShelfEvents.CountAsync()).ShouldBe(0);
        var saved = await nextNight.Series.Include(candidate => candidate.Volumes).SingleAsync(candidate => candidate.Id == repointedId);
        saved.MangaUpdatesId.ShouldBe(NewRecord);
        saved.Volumes.Count(volume => volume.IsReleased).ShouldBe(10);
    }

    private static SeriesSyncService CreateService(LnShelfDbContext dbContext, int? sourceCount) =>
        CreateService(dbContext, sourceCount is { } count ? ReleaseSource((MangaUpdatesId, count)) : ReleaseSource());

    private static IReleaseSource ReleaseSource(params (long ExternalId, int VolumeCount)[] records)
    {
        var releaseSource = Substitute.For<IReleaseSource>();
        releaseSource.SourceName.Returns("MangaUpdates");
        releaseSource.GetSeriesAsync(Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns((ReleaseSeriesInfo?)null);

        foreach (var (externalId, volumeCount) in records)
        {
            releaseSource.GetSeriesAsync(externalId, Arg.Any<CancellationToken>())
                .Returns(new ReleaseSeriesInfo
                {
                    ExternalId = externalId,
                    Title = "Youjo Senki",
                    VolumeCount = volumeCount,
                    RawStatus = $"{volumeCount} Volumes (Ongoing)",
                });
        }

        return releaseSource;
    }

    private static SeriesSyncService CreateService(LnShelfDbContext dbContext, IReleaseSource releaseSource)
    {
        var metadata = Substitute.For<ISeriesMetadataSource>();
        metadata.GetSeriesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns((SeriesMetadata?)null);

        var enrichmentSource = NoEnrichment();
        enrichmentSource.SourceName.Returns("OpenLibrary");

        var refresher = new SeriesRefresher(
            Substitute.For<IReleaseSourceMatcher>(),
            releaseSource,
            new SeriesDescriptionHealer(metadata, NullLogger<SeriesDescriptionHealer>.Instance),
            new ReleaseCountWriter(releaseSource, NullLogger<ReleaseCountWriter>.Instance),
            new PublicationStatusWriter(releaseSource, NullLogger<PublicationStatusWriter>.Instance),
            VolumeEnrichmentPipeline.Create(NoCatalogue(), enrichmentSource, NoCandidates(), VolumeEnrichmentPipeline.Writer()),
            ChapterMapPipeline.NoSources(),
            TimeProvider.System);

        return new SeriesSyncService(
            dbContext,
            refresher,
            releaseSource,
            NoCatalogue(),
            enrichmentSource,
            new EnrichmentBudgetSizes(
                Options.Create(new OpenLibraryOptions { MaximumLookupsPerRun = 60, MaximumLookupsOnDemand = 6 }),
                Options.Create(new AppleBooksOptions())),
            TimeProvider.System,
            NullLogger<SeriesSyncService>.Instance);
    }

    private static async Task<Guid> SeedAsync(LnShelfDbContext dbContext, int volumeCount, bool synced)
    {
        var series = new Series
        {
            Id = Guid.CreateVersion7(),
            AniListId = 94846,
            MangaUpdatesId = MangaUpdatesId,
            TitleEnglish = "The Saga of Tanya the Evil",
            Format = Format.LightNovel,
            PublicationStatus = PublicationStatus.Ongoing,
            Description = "Already healed.",
            ReleaseSourceVolumeCount = synced ? volumeCount : null,
            LastSyncedAt = synced ? DateTimeOffset.Now.AddDays(-3) : null,
        };

        foreach (var number in Enumerable.Range(1, volumeCount))
        {
            series.Volumes.Add(new Volume { Id = Guid.CreateVersion7(), Number = number, IsReleased = synced });
        }

        dbContext.Series.Add(series);
        await dbContext.SaveChangesAsync();

        return series.Id;
    }

    private static LnShelfDbContext CreateDbContext() => CreateDbContext($"sync-events-{Guid.CreateVersion7()}");

    private static LnShelfDbContext CreateDbContext(string databaseName) =>
        new(new DbContextOptionsBuilder<LnShelfDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options, TestUsers.Current);
}
