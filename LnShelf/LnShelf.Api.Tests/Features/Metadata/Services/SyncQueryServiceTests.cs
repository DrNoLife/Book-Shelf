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

    // A brand-new user has no runs of their own; another user's sweep must not stand in
    // for it, and "none" must come back as null so the sidebar can say "Not synced yet".
    [Fact]
    public async Task GetLatestAsync_OnlyAnotherUsersRuns_ReturnsNull()
    {
        var databaseName = $"sync-query-{Guid.CreateVersion7()}";
        await using (var otherUser = CreateDbContext(databaseName, TestUsers.As(TestUsers.SomeoneElse)))
        {
            AddRun(otherUser, Night, coversWholeShelf: true, finished: true);
            await otherUser.SaveChangesAsync();
        }

        await using var dbContext = CreateDbContext(databaseName, TestUsers.Current);

        var latest = await new SyncQueryService(dbContext).GetLatestAsync(CancellationToken.None);

        latest.ShouldBeNull();
    }

    // The sidebar's count works out "settled by your choice" when it reads: the row is a log of
    // what the run did, and a choice made or reset afterwards has to show without rewriting it.
    [Fact]
    public async Task GetLatestAsync_DiscrepancyWithACurrentChoice_ReadsResolvedByChoiceAndNotToReview()
    {
        await using var dbContext = CreateDbContext();
        var series = new Series { Id = Guid.CreateVersion7(), TitleEnglish = "86", Format = Format.LightNovel };
        series.SourceChoices.Add(new SourceChoice
        {
            Id = Guid.CreateVersion7(),
            Field = SyncDiscrepancyField.ReleaseDate,
            VolumeNumber = 8,
            Source = MetadataSourceName.AppleBooks,
        });
        dbContext.Series.Add(series);
        AddRunWithDateDisagreement(dbContext, series);
        await dbContext.SaveChangesAsync();

        var latest = await new SyncQueryService(dbContext).GetLatestAsync(CancellationToken.None);

        var discrepancy = latest.ShouldNotBeNull().Discrepancies.ShouldHaveSingleItem();
        discrepancy.ResolvedWith.ShouldBe(MetadataSourceName.AppleBooks);
        discrepancy.ResolvedByChoice.ShouldBeTrue();
        discrepancy.NeedsReview.ShouldBeFalse();
    }

    [Fact]
    public async Task GetLatestAsync_DiscrepancyAfterTheChoiceWasReset_ReadsUnresolvedAgain()
    {
        await using var dbContext = CreateDbContext();
        var series = new Series { Id = Guid.CreateVersion7(), TitleEnglish = "86", Format = Format.LightNovel };
        dbContext.Series.Add(series);

        // Recorded while the user's pick stood; the pick has since been reset.
        AddRunWithDateDisagreement(dbContext, series, resolvedWith: MetadataSourceName.AppleBooks);
        await dbContext.SaveChangesAsync();

        var latest = await new SyncQueryService(dbContext).GetLatestAsync(CancellationToken.None);

        var discrepancy = latest.ShouldNotBeNull().Discrepancies.ShouldHaveSingleItem();
        discrepancy.ResolvedWith.ShouldBeNull();
        discrepancy.ResolvedByChoice.ShouldBeFalse();
        discrepancy.NeedsReview.ShouldBeTrue();
    }

    // The catalogue pass and the by-ISBN pass each record a row for the same volume's date.
    // That is one item for whoever reads the footer, not two (RecordedDisagreement.FromRows).
    [Fact]
    public async Task GetLatestAsync_TwoRowsForOneVolumesDate_ReadAsOneItemToReview()
    {
        await using var dbContext = CreateDbContext();
        var series = new Series { Id = Guid.CreateVersion7(), TitleEnglish = "86", Format = Format.LightNovel };
        dbContext.Series.Add(series);
        var run = AddRun(dbContext, Night, coversWholeShelf: true, finished: true);

        foreach (var source in new[] { MetadataSourceName.AppleBooks, MetadataSourceName.OpenLibrary })
        {
            run.Discrepancies.Add(new SyncDiscrepancy
            {
                Id = Guid.CreateVersion7(),
                SeriesId = series.Id,
                SeriesTitle = series.TitleEnglish,
                Field = SyncDiscrepancyField.ReleaseDate,
                VolumeNumber = 8,
            }
                .Says(MetadataSourceName.MangaUpdates, "in print")
                .Says(source, "2099-03-09"));
        }

        await dbContext.SaveChangesAsync();

        var latest = await new SyncQueryService(dbContext).GetLatestAsync(CancellationToken.None);

        var discrepancy = latest.ShouldNotBeNull().Discrepancies.ShouldHaveSingleItem();
        discrepancy.Values.Select(value => value.Source).ShouldBe(
            [MetadataSourceName.MangaUpdates, MetadataSourceName.AppleBooks, MetadataSourceName.OpenLibrary],
            ignoreOrder: true);
    }

    private static void AddRunWithDateDisagreement(
        LnShelfDbContext dbContext, Series series, MetadataSourceName? resolvedWith = null)
    {
        var run = AddRun(dbContext, Night, coversWholeShelf: true, finished: true);
        run.Discrepancies.Add(new SyncDiscrepancy
        {
            Id = Guid.CreateVersion7(),
            SeriesId = series.Id,
            SeriesTitle = series.TitleEnglish,
            Field = SyncDiscrepancyField.ReleaseDate,
            VolumeNumber = 8,
            ResolvedWith = resolvedWith,
            ResolvedByChoice = resolvedWith is not null,
        }
            .Says(MetadataSourceName.MangaUpdates, "in print")
            .Says(MetadataSourceName.AppleBooks, "2099-03-09"));
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
        CreateDbContext($"sync-query-{Guid.CreateVersion7()}", TestUsers.Current);

    private static LnShelfDbContext CreateDbContext(string databaseName, ICurrentUser currentUser) =>
        new(new DbContextOptionsBuilder<LnShelfDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options, currentUser);
}
