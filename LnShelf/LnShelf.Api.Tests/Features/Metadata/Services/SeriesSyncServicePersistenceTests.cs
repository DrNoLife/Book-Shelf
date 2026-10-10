using LnShelf.Api.Tests.Data.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using static LnShelf.Api.Tests.Features.Metadata.Services.SeriesSyncServiceTests;

namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// What a run leaves in the database when it does not go to plan: each series saved on its
/// own, a failed one discarded whole, and the run row always closed.
/// </summary>
// Every assertion reads through a second context over the same in-memory database. The run's
// own context would answer from its tracked entities, which shows what the run holds in
// memory, not what it saved — and the difference is the whole subject here.
public sealed class SeriesSyncServicePersistenceTests
{
    private readonly InMemoryDatabaseRoot _databaseRoot = new();
    private readonly string _databaseName = $"sync-persistence-{Guid.CreateVersion7()}";

    [Fact]
    public async Task RunAsync_ASeriesThatThrowsHalfway_LeavesNoneOfItsChangesSaved()
    {
        await using var dbContext = CreateDbContext();
        await SeedSeriesAsync(dbContext, volumeCount: 3, titleEnglish: "Half Done", aniListId: 1);
        await SeedSeriesAsync(dbContext, volumeCount: 3, titleEnglish: "Survives", aniListId: 2);

        // A refresh that has written to the series — a field, a new volume, a discrepancy on
        // the run — and then fails. Before, all three rode along on the next series' save.
        var matcher = Substitute.For<IReleaseSourceMatcher>();
        matcher.MatchAsync(
                Arg.Is<Series>(series => series.TitleEnglish == "Half Done"),
                Arg.Any<IReadOnlySet<long>>(),
                Arg.Any<CancellationToken>())
            .Returns<Task<ReleaseSourceMatch>>(callInfo =>
            {
                var series = callInfo.Arg<Series>();
                series.TitleRomaji = "Half applied";
                series.Volumes.Add(new Volume { Id = Guid.CreateVersion7(), Number = 4 });
                throw new HttpRequestException("MangaUpdates went away mid-refresh");
            });
        matcher.MatchAsync(
                Arg.Is<Series>(series => series.TitleEnglish == "Survives"),
                Arg.Any<IReadOnlySet<long>>(),
                Arg.Any<CancellationToken>())
            .Returns(Linked(volumeCount: 3));

        var run = await CreateService(dbContext, matcher).RunAsync(CancellationToken.None);

        run.Failures.ShouldBe(1);
        run.SeriesRefreshed.ShouldBe(1);

        await using var saved = CreateDbContext();
        var halfDone = await saved.Series.Include(series => series.Volumes)
            .SingleAsync(series => series.TitleEnglish == "Half Done");
        halfDone.TitleRomaji.ShouldBe("Youjo Senki");
        halfDone.Volumes.Count.ShouldBe(3);

        (await saved.Series.SingleAsync(series => series.TitleEnglish == "Survives"))
            .MangaUpdatesId.ShouldBe(MangaUpdatesId);
    }

    [Fact]
    public async Task RunAsync_ASaveTheDatabaseRefuses_CountsAsThatSeriesFailureAndKeepsEarlierWork()
    {
        // Seeded through a context without the interceptor, so only the run's saves can fail.
        await using (var seeding = CreateDbContext())
        {
            await SeedSeriesAsync(seeding, volumeCount: 3, titleEnglish: "Already Saved", aniListId: 1);
            await SeedSeriesAsync(seeding, volumeCount: 3, titleEnglish: "Refused", aniListId: 2);
        }

        // Refuses whichever save carries a change to "Refused" — the way a unique index would.
        await using var dbContext = CreateDbContext(new RefusingSaveInterceptor(
            context => context.ChangeTracker.Entries<Series>().Any(entry =>
                entry.State == EntityState.Modified && entry.Entity.TitleEnglish == "Refused"),
            RefusingSaveInterceptor.UniqueViolation));

        var run = await CreateService(dbContext, Linked(volumeCount: 3)).RunAsync(CancellationToken.None);

        run.SeriesRefreshed.ShouldBe(1);
        run.Failures.ShouldBe(1);
        run.FinishedAt.ShouldNotBeNull();

        await using var saved = CreateDbContext();

        // The series before it is saved, not lost along with the one the database refused.
        (await saved.Series.SingleAsync(series => series.TitleEnglish == "Already Saved"))
            .MangaUpdatesId.ShouldBe(MangaUpdatesId);
        (await saved.Series.SingleAsync(series => series.TitleEnglish == "Refused"))
            .MangaUpdatesId.ShouldBeNull();

        var savedRun = await saved.SyncRuns.SingleAsync();
        savedRun.Failures.ShouldBe(1);
        savedRun.SeriesRefreshed.ShouldBe(1);
        savedRun.Summary.ShouldContain("1 failed");
    }

    [Fact]
    public async Task RunAsync_TheCallerCancels_StillFinishesTheRunRowAndSaysItWasInterrupted()
    {
        await using var dbContext = CreateDbContext();
        await SeedSeriesAsync(dbContext, volumeCount: 3, titleEnglish: "A First", aniListId: 1);
        await SeedSeriesAsync(dbContext, volumeCount: 3, titleEnglish: "B Interrupted", aniListId: 2);

        using var cancellation = new CancellationTokenSource();

        var matcher = Substitute.For<IReleaseSourceMatcher>();
        matcher.MatchAsync(
                Arg.Is<Series>(series => series.TitleEnglish == "A First"),
                Arg.Any<IReadOnlySet<long>>(),
                Arg.Any<CancellationToken>())
            .Returns(Linked(volumeCount: 3));
        matcher.MatchAsync(
                Arg.Is<Series>(series => series.TitleEnglish == "B Interrupted"),
                Arg.Any<IReadOnlySet<long>>(),
                Arg.Any<CancellationToken>())
            .Returns<Task<ReleaseSourceMatch>>(_ =>
            {
                cancellation.Cancel();
                throw new OperationCanceledException(cancellation.Token);
            });

        await Should.ThrowAsync<OperationCanceledException>(
            () => CreateService(dbContext, matcher).RunAsync(cancellation.Token));

        await using var saved = CreateDbContext();

        // A shutdown mid-run used to leave the row "in progress" forever and the night's work
        // unsaved. Now the row is closed, says why, and the first series' refresh survived.
        var run = await saved.SyncRuns.SingleAsync();
        run.FinishedAt.ShouldNotBeNull();
        run.Summary.ShouldStartWith("Interrupted after 1 series refreshed");

        (await saved.Series.SingleAsync(series => series.TitleEnglish == "A First"))
            .MangaUpdatesId.ShouldBe(MangaUpdatesId);
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
