using LnShelf.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// Which run the sidebar prints: the last finished sweep of the whole shelf, because its
/// items to review are what the footer exists to show.
/// </summary>
public sealed class SyncQueryServiceTests
{
    private static readonly DateTimeOffset Night = new(2026, 10, 9, 4, 0, 0, TimeSpan.Zero);

    // Adding a series refreshes that one series in a run of its own, which used to replace
    // the nightly run and its discrepancies in the sidebar (audit 2026-10-09).
    [Fact]
    public async Task GetLatestAsync_OneSeriesRefreshAfterTheSweep_StillReturnsTheSweep()
    {
        await using var dbContext = CreateDbContext();
        var sweep = AddRun(dbContext, Night, coversWholeShelf: true, finished: true);
        AddRun(dbContext, Night.AddHours(8), coversWholeShelf: false, finished: true);
        await dbContext.SaveChangesAsync();

        var latest = await new SyncQueryService(dbContext).GetLatestAsync(CancellationToken.None);

        latest.ShouldNotBeNull().RunId.ShouldBe(sweep.Id);
    }

    [Fact]
    public async Task GetLatestAsync_SweepStillRunning_ReturnsThePreviousFinishedSweep()
    {
        await using var dbContext = CreateDbContext();
        var previous = AddRun(dbContext, Night, coversWholeShelf: true, finished: true);
        AddRun(dbContext, Night.AddDays(1), coversWholeShelf: true, finished: false);
        await dbContext.SaveChangesAsync();

        var latest = await new SyncQueryService(dbContext).GetLatestAsync(CancellationToken.None);

        latest.ShouldNotBeNull().RunId.ShouldBe(previous.Id);
    }

    [Fact]
    public async Task GetLatestAsync_NoSweepYet_FallsBackToTheLatestRun()
    {
        await using var dbContext = CreateDbContext();
        var refresh = AddRun(dbContext, Night, coversWholeShelf: false, finished: true);
        await dbContext.SaveChangesAsync();

        var latest = await new SyncQueryService(dbContext).GetLatestAsync(CancellationToken.None);

        latest.ShouldNotBeNull().RunId.ShouldBe(refresh.Id);
    }

    private static SyncRun AddRun(
        LnShelfDbContext dbContext,
        DateTimeOffset startedAt,
        bool coversWholeShelf,
        bool finished)
    {
        var run = new SyncRun
        {
            Id = Guid.CreateVersion7(),
            StartedAt = startedAt,
            FinishedAt = finished ? startedAt.AddMinutes(30) : null,
            CoversWholeShelf = coversWholeShelf,
            Summary = coversWholeShelf ? "24 series refreshed" : "Overlord refreshed",
        };

        dbContext.SyncRuns.Add(run);
        return run;
    }

    private static LnShelfDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<LnShelfDbContext>()
            .UseInMemoryDatabase($"sync-query-{Guid.CreateVersion7()}")
            .Options);
}
