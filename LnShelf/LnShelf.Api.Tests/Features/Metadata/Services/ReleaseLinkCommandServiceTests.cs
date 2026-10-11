using Microsoft.EntityFrameworkCore;
using static LnShelf.Api.Tests.Features.Metadata.Services.SourceChoiceTestContext;

namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// Linking a series to its release source by hand (D8): what is saved, what is refused, and
/// that the link survives a refresh that cannot run.
/// </summary>
public sealed class ReleaseLinkCommandServiceTests
{
    private const long FreeRecord = 17992364412;
    private static readonly DateTimeOffset Night = new(2026, 10, 9, 4, 0, 0, TimeSpan.Zero);

    private readonly IReleaseSource _releaseSource = ReleaseSourceKnowing(FreeRecord);
    private readonly ISeriesSyncService _syncService = RefreshingSync();
    private readonly SyncRunGate _gate = new();

    [Fact]
    public async Task LinkAsync_AFreeRecord_SavesTheLinkAndRefreshes()
    {
        await using var dbContext = CreateDbContext();
        var series = await SaveUnlinkedSeriesAsync(dbContext);

        var linked = await CreateService(dbContext).LinkAsync(series.Id, Request(FreeRecord), CancellationToken.None);

        linked.ShouldNotBeNull().Refreshed.ShouldBeTrue();
        linked.Series.MangaUpdatesId.ShouldBe(FreeRecord);
        await _syncService.Received(1).RunForSeriesAsync(series.Id, Arg.Any<CancellationToken>());

        (await dbContext.Series.AsNoTracking().SingleAsync()).MangaUpdatesId.ShouldBe(FreeRecord);
    }

    // Answered in every run that asked, and read back as settled by the user — so the sidebar's
    // "to review" count drops without waiting for the next sweep.
    [Fact]
    public async Task LinkAsync_OpenLinkQuestions_AreAnsweredByTheUser()
    {
        await using var dbContext = CreateDbContext();
        var series = await SaveUnlinkedSeriesAsync(dbContext);
        await SaveRunAsync(dbContext, Night.AddDays(-1), discrepancies: [LinkQuestion(series)]);
        await SaveRunAsync(dbContext, Night, discrepancies: [LinkQuestion(series)]);

        await CreateService(dbContext).LinkAsync(series.Id, Request(FreeRecord), CancellationToken.None);

        var rows = await dbContext.SyncRuns.AsNoTracking().SelectMany(run => run.Discrepancies).ToListAsync();
        rows.Count.ShouldBe(2);
        rows.ShouldAllBe(row => row.ResolvedWith == MetadataSourceName.MangaUpdates && row.ResolvedByChoice);

        var latest = (await new SyncQueryService(dbContext).GetLatestAsync(CancellationToken.None)).ShouldNotBeNull();
        latest.Discrepancies.ShouldHaveSingleItem().NeedsReview.ShouldBeFalse();
    }

    [Fact]
    public async Task LinkAsync_ARecordAnotherSeriesHolds_IsRefusedAndNothingChanges()
    {
        await using var dbContext = CreateDbContext();
        var series = await SaveUnlinkedSeriesAsync(dbContext);
        var holder = CreateSeries();
        holder.AniListId = AniListId + 1;
        holder.MangaUpdatesId = FreeRecord;
        await SaveSeriesAsync(dbContext, holder);

        await Should.ThrowAsync<ConflictException>(() =>
            CreateService(dbContext).LinkAsync(series.Id, Request(FreeRecord), CancellationToken.None));

        (await dbContext.Series.AsNoTracking().SingleAsync(candidate => candidate.Id == series.Id))
            .MangaUpdatesId.ShouldBeNull();
    }

    // A hand-added series has nothing to refresh against, and one added from MangaUpdates is
    // linked by construction.
    [Fact]
    public async Task LinkAsync_ASeriesWithoutAnAniListId_IsRefused()
    {
        await using var dbContext = CreateDbContext();
        var series = CreateSeries();
        series.AniListId = null;
        series.MangaUpdatesId = null;
        await SaveSeriesAsync(dbContext, series);

        await Should.ThrowAsync<ConflictException>(() =>
            CreateService(dbContext).LinkAsync(series.Id, Request(FreeRecord), CancellationToken.None));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    [InlineData(45348350266)] // Not a record the release source knows.
    public async Task LinkAsync_AnIdWithNoRecord_IsRefused(long mangaUpdatesId)
    {
        await using var dbContext = CreateDbContext();
        var series = await SaveUnlinkedSeriesAsync(dbContext);

        await Should.ThrowAsync<InvalidInputException>(() =>
            CreateService(dbContext).LinkAsync(series.Id, Request(mangaUpdatesId), CancellationToken.None));

        (await dbContext.Series.AsNoTracking().SingleAsync()).MangaUpdatesId.ShouldBeNull();
    }

    [Theory]
    [InlineData("Bad gateway")]
    [InlineData("timed out")]
    [InlineData("timeout")]
    public async Task LinkAsync_TheReleaseSourceIsDown_SaysSoAsA503(string failure)
    {
        await using var dbContext = CreateDbContext();
        var series = await SaveUnlinkedSeriesAsync(dbContext);
        _releaseSource.GetSeriesAsync(FreeRecord, Arg.Any<CancellationToken>())
            .Returns<ReleaseSeriesInfo?>(_ => throw ReleaseLinkQueryServiceTests.SourceFailure(failure));

        await Should.ThrowAsync<MetadataSourceUnavailableException>(() =>
            CreateService(dbContext).LinkAsync(series.Id, Request(FreeRecord), CancellationToken.None));
    }

    // Busy is not a failure: the link is saved, and the sync picks it up (SeriesSyncServiceTests).
    [Fact]
    public async Task LinkAsync_ASyncIsAlreadyRunning_SavesTheLinkWithoutRefreshing()
    {
        await using var dbContext = CreateDbContext();
        var series = await SaveUnlinkedSeriesAsync(dbContext);
        using var running = _gate.TryEnter(TestUsers.Owner);

        var linked = await CreateService(dbContext).LinkAsync(series.Id, Request(FreeRecord), CancellationToken.None);

        linked.ShouldNotBeNull().Refreshed.ShouldBeFalse();
        linked.Series.MangaUpdatesId.ShouldBe(FreeRecord);
        await _syncService.DidNotReceiveWithAnyArgs().RunForSeriesAsync(default, default);
    }

    [Fact]
    public async Task LinkAsync_TheRefreshFails_KeepsTheLinkAndReleasesTheGate()
    {
        await using var dbContext = CreateDbContext();
        var series = await SaveUnlinkedSeriesAsync(dbContext);
        _syncService.RunForSeriesAsync(series.Id, Arg.Any<CancellationToken>())
            .Returns<SyncRun?>(_ => throw new InvalidOperationException("the run could not start"));

        var linked = await CreateService(dbContext).LinkAsync(series.Id, Request(FreeRecord), CancellationToken.None);

        linked.ShouldNotBeNull().Refreshed.ShouldBeFalse();
        (await dbContext.Series.AsNoTracking().SingleAsync()).MangaUpdatesId.ShouldBe(FreeRecord);
        _gate.TryEnter(TestUsers.Owner).ShouldNotBeNull().Dispose();
    }

    // A different record's volumes are not news: the refresh after a re-link runs as a first one.
    [Fact]
    public async Task LinkAsync_ASeriesLinkedToAnotherRecord_IsRefreshedAsIfForTheFirstTime()
    {
        await using var dbContext = CreateDbContext();
        var series = await SaveSeriesAsync(dbContext);

        await CreateService(dbContext).LinkAsync(series.Id, Request(FreeRecord), CancellationToken.None);

        var saved = await dbContext.Series.AsNoTracking().SingleAsync();
        saved.MangaUpdatesId.ShouldBe(FreeRecord);
        saved.LastSyncedAt.ShouldBeNull();
    }

    // The main use of the endpoint: a series the matcher left unlinked still carries its last
    // sync, and its volumes were never flagged released by any count.
    [Fact]
    public async Task LinkAsync_AnUnlinkedSeriesThatWasSyncedBefore_IsRefreshedAsIfForTheFirstTime()
    {
        await using var dbContext = CreateDbContext();
        var series = await SaveUnlinkedSeriesAsync(dbContext);
        series.LastSyncedAt.ShouldNotBeNull();

        await CreateService(dbContext).LinkAsync(series.Id, Request(FreeRecord), CancellationToken.None);

        (await dbContext.Series.AsNoTracking().SingleAsync()).LastSyncedAt.ShouldBeNull();
    }

    [Fact]
    public async Task LinkAsync_TheSameRecordAgain_KeepsTheLastSync()
    {
        await using var dbContext = CreateDbContext();
        var series = CreateSeries();
        series.MangaUpdatesId = FreeRecord;
        await SaveSeriesAsync(dbContext, series);

        await CreateService(dbContext).LinkAsync(series.Id, Request(FreeRecord), CancellationToken.None);

        (await dbContext.Series.AsNoTracking().SingleAsync()).LastSyncedAt.ShouldBe(DateTimeOffset.UnixEpoch);
    }

    [Fact]
    public async Task LinkAsync_AnotherUsersSeries_ReturnsNull()
    {
        var databaseName = $"release-link-{Guid.CreateVersion7()}";
        Series theirs;
        await using (var otherUser = CreateDbContext(databaseName, TestUsers.As(TestUsers.SomeoneElse)))
        {
            theirs = await SaveUnlinkedSeriesAsync(otherUser);
        }

        await using var dbContext = CreateDbContext(databaseName, TestUsers.Current);

        var linked = await CreateService(dbContext).LinkAsync(theirs.Id, Request(FreeRecord), CancellationToken.None);

        linked.ShouldBeNull();
        await _syncService.DidNotReceiveWithAnyArgs().RunForSeriesAsync(default, default);
    }

    private static async Task<Series> SaveUnlinkedSeriesAsync(LnShelfDbContext dbContext)
    {
        var series = CreateSeries();
        series.MangaUpdatesId = null;

        return await SaveSeriesAsync(dbContext, series);
    }

    private static SyncDiscrepancy LinkQuestion(Series series) =>
        SyncDiscrepancies.SeriesLink(series, new ReleaseSourceMatch
        {
            Outcome = ReleaseMatchOutcome.LowConfidence,
            Score = 61,
            Explanation = "Closest was 'Eighty Six' at 61.",
        });

    private static LinkReleaseSourceRequest Request(long mangaUpdatesId) => new() { MangaUpdatesId = mangaUpdatesId };

    private static IReleaseSource ReleaseSourceKnowing(long externalId)
    {
        var releaseSource = Substitute.For<IReleaseSource>();
        releaseSource.SourceName.Returns("MangaUpdates");
        releaseSource.GetSeriesAsync(Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns((ReleaseSeriesInfo?)null);
        releaseSource.GetSeriesAsync(externalId, Arg.Any<CancellationToken>())
            .Returns(new ReleaseSeriesInfo { ExternalId = externalId, Title = "86" });

        return releaseSource;
    }

    private static ISeriesSyncService RefreshingSync()
    {
        var syncService = Substitute.For<ISeriesSyncService>();
        syncService.RunForSeriesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new SyncRun { Id = Guid.CreateVersion7(), StartedAt = Night });

        return syncService;
    }

    private ReleaseLinkCommandService CreateService(LnShelfDbContext dbContext) =>
        new(
            dbContext,
            _releaseSource,
            _syncService,
            _gate,
            TestUsers.Current,
            new SeriesDetailQueryService(dbContext, TimeProvider.System),
            NullLogger<ReleaseLinkCommandService>.Instance);
}
