using LnShelf.Api.Tests.Data.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace LnShelf.Api.Tests.Features.Shelf.Services;

/// <summary>
/// Adding a series: twice at the same moment, where the request that loses the race answers
/// as if the series had already been there (which by then it is), and with a "Start with",
/// where the reader's place in the series lands in the same save as the series.
/// </summary>
public sealed class AddSeriesPipelineTests
{
    private const int AniListId = 85470;
    private const string Title = "Mushoku Tensei: Jobless Reincarnation";

    private readonly InMemoryDatabaseRoot _databaseRoot = new();
    private readonly string _databaseName = $"add-series-race-{Guid.CreateVersion7()}";

    [Fact]
    public async Task RunAsync_AConcurrentAddWinsTheRace_ReturnsTheWinnersSeriesAsAlreadyTracked()
    {
        var winnerId = Guid.CreateVersion7();
        var raceLost = false;

        // The in-memory provider has no unique index, so the race is staged: at this request's
        // save, the other request's row lands first and the save fails the way Postgres fails it.
        bool LoseTheRace(DbContext context)
        {
            if (raceLost || !context.ChangeTracker.Entries<Series>().Any(entry => entry.State == EntityState.Added))
            {
                return false;
            }

            using var otherRequest = CreateDbContext();
            otherRequest.Series.Add(new Series
            {
                Id = winnerId,
                AniListId = AniListId,
                TitleEnglish = Title,
                Format = Format.LightNovel,
                PublicationStatus = PublicationStatus.Completed,
            });
            otherRequest.SaveChanges();

            raceLost = true;
            return true;
        }

        await using var dbContext = CreateDbContext(
            new RefusingSaveInterceptor(LoseTheRace, RefusingSaveInterceptor.UniqueViolation));

        // With a start-with, so the loser has volume state pending as well as the series.
        var response = await CreatePipeline(dbContext).RunAsync(
            StartWith(read: 2, owned: 3), CancellationToken.None);

        // What the sequential path answers for a series already on the shelf — not a 500.
        response.ShouldNotBeNull().SeriesId.ShouldBe(winnerId);
        response.AlreadyTracked.ShouldBeTrue();

        // And the loser's own copy is gone from the context, so nothing re-inserts it later.
        dbContext.ChangeTracker.Entries().ShouldNotContain(entry => entry.State == EntityState.Added);

        await using var saved = CreateDbContext();
        (await saved.Series.CountAsync()).ShouldBe(1);

        // The winner's state is the winner's: the loser's start-with wrote nothing.
        (await saved.VolumeUserStates.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_StartWithReadAndOwned_MarksTheVolumesWithoutInventingDates()
    {
        await using var dbContext = CreateDbContext();

        var response = await CreatePipeline(dbContext, volumeCount: 5).RunAsync(
            StartWith(read: 2, owned: 3), CancellationToken.None);

        response.ShouldNotBeNull().AlreadyTracked.ShouldBeFalse();

        var series = await LoadAsync();
        ReadNumbers(series).ShouldBe([1, 2]);
        OwnedNumbers(series).ShouldBe([1, 2, 3]);

        // When they were read or bought is exactly what the add does not know.
        var states = series.Volumes.Select(volume => volume.UserState).OfType<VolumeUserState>().ToList();
        states.ShouldAllBe(state => state.ReadAt == null && state.PurchasedAt == null);
        states.Where(state => state.Owned).ShouldAllBe(state => state.OwnedFormat == OwnedFormat.Physical);

        series.UserState.ShouldNotBeNull().TrackingStatus.ShouldBe(TrackingStatus.Reading);
        series.UserState.LastReadAt.ShouldBeNull();
    }

    [Fact]
    public async Task RunAsync_StartWithEveryVolumeReadOnAFinishedSeries_LandsOnCompleted()
    {
        await using var dbContext = CreateDbContext();

        // The source reports the series Completed, and read volumes count as owned, so all
        // five are out and read.
        await CreatePipeline(dbContext, volumeCount: 5).RunAsync(
            StartWith(read: 5, owned: 5), CancellationToken.None);

        var series = await LoadAsync();
        series.UserState.ShouldNotBeNull().TrackingStatus.ShouldBe(TrackingStatus.Completed);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(0, 0)]
    public async Task RunAsync_NoStartWith_AddsTheSeriesUntouchedOnPlanToStart(int? read, int? owned)
    {
        await using var dbContext = CreateDbContext();

        await CreatePipeline(dbContext).RunAsync(StartWith(read, owned), CancellationToken.None);

        var series = await LoadAsync();
        series.Volumes.ShouldAllBe(volume => volume.UserState == null);
        series.UserState.ShouldNotBeNull().TrackingStatus.ShouldBe(TrackingStatus.PlanToStart);
    }

    [Fact]
    public async Task RunAsync_OwnedOnly_StaysOnPlanToStart()
    {
        await using var dbContext = CreateDbContext();

        await CreatePipeline(dbContext).RunAsync(StartWith(read: 0, owned: 2), CancellationToken.None);

        var series = await LoadAsync();
        ReadNumbers(series).ShouldBeEmpty();
        OwnedNumbers(series).ShouldBe([1, 2]);
        series.UserState.ShouldNotBeNull().TrackingStatus.ShouldBe(TrackingStatus.PlanToStart);
    }

    /// <summary>A read volume counts as one on the shelf, so owned is raised to read.</summary>
    [Fact]
    public async Task RunAsync_OwnedBelowRead_OwnsEveryReadVolume()
    {
        await using var dbContext = CreateDbContext();

        await CreatePipeline(dbContext).RunAsync(StartWith(read: 3, owned: 1), CancellationToken.None);

        var series = await LoadAsync();
        ReadNumbers(series).ShouldBe([1, 2, 3]);
        OwnedNumbers(series).ShouldBe([1, 2, 3]);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    public async Task RunAsync_ANegativeCount_IsRefusedBeforeTheSourceIsAsked(int read, int owned)
    {
        await using var dbContext = CreateDbContext();
        var metadataSource = MetadataSource(volumeCount: 3);

        await Should.ThrowAsync<InvalidInputException>(() =>
            CreatePipeline(dbContext, metadataSource).RunAsync(StartWith(read, owned), CancellationToken.None));

        await metadataSource.DidNotReceiveWithAnyArgs().GetSeriesAsync(default, default);
    }

    [Theory]
    [InlineData(4, 0)]
    [InlineData(0, 4)]
    public async Task RunAsync_MoreThanTheVolumeCount_IsRefusedAndNothingIsSaved(int read, int owned)
    {
        await using var dbContext = CreateDbContext();

        await Should.ThrowAsync<InvalidInputException>(() =>
            CreatePipeline(dbContext, volumeCount: 3).RunAsync(StartWith(read, owned), CancellationToken.None));

        await using var saved = CreateDbContext();
        (await saved.Series.CountAsync()).ShouldBe(0);
    }

    /// <summary>
    /// A source with no volume count adds a series with no volumes; the ones the reader says
    /// they have are created local-only, the way the import creates them.
    /// </summary>
    [Fact]
    public async Task RunAsync_NoVolumeCount_CreatesTheStartedVolumesAsLocalOnly()
    {
        await using var dbContext = CreateDbContext();

        await CreatePipeline(dbContext, volumeCount: 0).RunAsync(StartWith(read: 2, owned: 4), CancellationToken.None);

        var series = await LoadAsync();
        series.Volumes.Select(volume => volume.Number).Order().ShouldBe([1, 2, 3, 4]);
        series.Volumes.ShouldAllBe(volume => volume.IsLocalOnly);
        ReadNumbers(series).ShouldBe([1, 2]);
        OwnedNumbers(series).ShouldBe([1, 2, 3, 4]);
    }

    [Fact]
    public async Task RunAsync_NoVolumeCount_IsCappedAtTheSharedLimit()
    {
        await using var dbContext = CreateDbContext();
        var overLimit = AddSeriesRequest.InitialCountLimitWithoutVolumeCount + 1;

        await Should.ThrowAsync<InvalidInputException>(() =>
            CreatePipeline(dbContext, volumeCount: 0).RunAsync(StartWith(read: 0, owned: overLimit), CancellationToken.None));
    }

    /// <summary>
    /// A start-with sent from a stale search page for a series already on the shelf must not
    /// overwrite what the reader has recorded there since.
    /// </summary>
    [Fact]
    public async Task RunAsync_AlreadyTracked_LeavesTheReadersStateAlone()
    {
        await using (var seed = CreateDbContext())
        {
            var existing = new Series
            {
                Id = Guid.CreateVersion7(),
                AniListId = AniListId,
                TitleEnglish = Title,
                Format = Format.LightNovel,
                PublicationStatus = PublicationStatus.Completed,
                UserState = new SeriesUserState { TrackingStatus = TrackingStatus.Dropped },
            };
            existing.Volumes.Add(new Volume { Id = Guid.CreateVersion7(), Number = 1 });
            existing.Volumes.Add(new Volume { Id = Guid.CreateVersion7(), Number = 2 });
            seed.Series.Add(existing);
            await seed.SaveChangesAsync();
        }

        await using var dbContext = CreateDbContext();
        var response = await CreatePipeline(dbContext).RunAsync(StartWith(read: 2, owned: 2), CancellationToken.None);

        response.ShouldNotBeNull().AlreadyTracked.ShouldBeTrue();

        var series = await LoadAsync();
        series.Volumes.ShouldAllBe(volume => volume.UserState == null);
        series.UserState.ShouldNotBeNull().TrackingStatus.ShouldBe(TrackingStatus.Dropped);
    }

    private static AddSeriesRequest StartWith(int? read, int? owned) => new()
    {
        AniListId = AniListId,
        Format = Format.LightNovel,
        InitialReadCount = read,
        InitialOwnedCount = owned,
    };

    private static int[] ReadNumbers(Series series) =>
    [
        .. series.Volumes
            .Where(volume => volume.UserState?.ReadStatus is ReadStatus.Read)
            .Select(volume => (int)volume.Number)
            .Order(),
    ];

    private static int[] OwnedNumbers(Series series) =>
    [
        .. series.Volumes
            .Where(volume => volume.UserState?.Owned is true)
            .Select(volume => (int)volume.Number)
            .Order(),
    ];

    private async Task<Series> LoadAsync()
    {
        await using var saved = CreateDbContext();

        return await saved.Series
            .Include(series => series.UserState)
            .Include(series => series.Volumes).ThenInclude(volume => volume.UserState)
            .SingleAsync();
    }

    private static AddSeriesPipeline CreatePipeline(LnShelfDbContext dbContext, int volumeCount = 3) =>
        CreatePipeline(dbContext, MetadataSource(volumeCount));

    private static ISeriesMetadataSource MetadataSource(int volumeCount)
    {
        var metadataSource = Substitute.For<ISeriesMetadataSource>();
        metadataSource.GetSeriesAsync(AniListId, Arg.Any<CancellationToken>())
            .Returns(new SeriesMetadata
            {
                ExternalId = AniListId,
                TitleEnglish = Title,
                Format = Format.LightNovel,
                PublicationStatus = PublicationStatus.Completed,
                Volumes = [.. Enumerable.Range(1, volumeCount).Select(number => new VolumeMetadata { Number = number })],
            });

        return metadataSource;
    }

    private static AddSeriesPipeline CreatePipeline(LnShelfDbContext dbContext, ISeriesMetadataSource metadataSource)
    {
        return new AddSeriesPipeline(
            dbContext,
            [
                new AniListAddSeriesFlow(
                    metadataSource,
                    Substitute.For<ISeriesCoverCache>(),
                    new AddTimeReleaseLink(
                        SeriesCommandServiceTests.NoReleaseLink(),
                        dbContext,
                        NullLogger<AddTimeReleaseLink>.Instance)),
            ],
            Substitute.For<IIsbnBackfill>(),
            Substitute.For<ISeriesSyncService>(),
            TestUsers.Limits(dbContext), TimeProvider.System,
            NullLogger<AddSeriesPipeline>.Instance);
    }

    private LnShelfDbContext CreateDbContext(RefusingSaveInterceptor? interceptor = null)
    {
        var options = new DbContextOptionsBuilder<LnShelfDbContext>()
            .UseInMemoryDatabase(_databaseName, _databaseRoot);

        if (interceptor is not null)
        {
            options.AddInterceptors(interceptor);
        }

        return new LnShelfDbContext(options.Options, TestUsers.Current);
    }
}
