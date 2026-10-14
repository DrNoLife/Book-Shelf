using static LnShelf.Api.Tests.Features.Metadata.Services.SourceChoiceTestContext;
using static LnShelf.Api.Tests.Features.Metadata.Services.VolumeEnrichmentTestContext;

namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// The series page's side-by-side view: which disagreements it lists, what it says each source
/// said, and which source it shows and why.
/// </summary>
public sealed class SourceConflictQueryServiceTests
{
    private static readonly DateTimeOffset Night = new(2026, 10, 9, 4, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetAsync_UnresolvedReleaseDate_ShowsMangaUpdatesWaitingOnYou()
    {
        await using var dbContext = CreateDbContext();
        var series = await SaveSeriesAsync(dbContext);
        await SaveRunAsync(dbContext, Night, discrepancies: [DateDisagreement(series)]);

        var conflicts = await new SourceConflictQueryService(dbContext).GetAsync(series.Id, CancellationToken.None);

        var conflict = conflicts.ShouldNotBeNull().ShouldHaveSingleItem();
        conflict.Field.ShouldBe(SyncDiscrepancyField.ReleaseDate);
        conflict.VolumeNumber.ShouldBe(8);
        conflict.Reason.ShouldBe(SourceDisplayReason.WaitingOnYou);
        conflict.NeedsReview.ShouldBeTrue();
        conflict.CanChoose.ShouldBeTrue();
        conflict.DisplayedSource.ShouldBe(MetadataSourceName.MangaUpdates);
        conflict.RecordedAt.ShouldBe(Night);

        conflict.Options.Single(option => option.IsDisplayed).Source.ShouldBe(MetadataSourceName.MangaUpdates);
        conflict.Options.Single(option => option.Source is MetadataSourceName.AppleBooks)
            .DisplayValue.ShouldBe("9 Mar 2099");
    }

    [Fact]
    public async Task GetAsync_WithAChoice_MarksItYourChoiceAndDisplayed()
    {
        await using var dbContext = CreateDbContext();
        var series = await SaveSeriesAsync(dbContext);
        series.SourceChoices.Add(Choice(SyncDiscrepancyField.ReleaseDate, 8, MetadataSourceName.AppleBooks));
        await SaveRunAsync(dbContext, Night, discrepancies: [DateDisagreement(series)]);

        var conflicts = await new SourceConflictQueryService(dbContext).GetAsync(series.Id, CancellationToken.None);

        var conflict = conflicts.ShouldNotBeNull().ShouldHaveSingleItem();
        conflict.Reason.ShouldBe(SourceDisplayReason.YourChoice);
        conflict.NeedsReview.ShouldBeFalse();
        conflict.DisplayedSource.ShouldBe(MetadataSourceName.AppleBooks);
        conflict.Options.Single(option => option.IsDisplayed).SourceName.ShouldBe("Apple Books");
        conflict.Explanation.ShouldContain("Apple Books");
    }

    // The choice outlives the sweep that showed its disagreement: once the sources agree again
    // nothing is recorded, and the page must still offer Reset.
    [Fact]
    public async Task GetAsync_ChoiceWithNoRowInTheLatestSweep_UsesTheNewestRecordedRow()
    {
        await using var dbContext = CreateDbContext();
        var series = await SaveSeriesAsync(dbContext);
        series.SourceChoices.Add(Choice(SyncDiscrepancyField.ReleaseDate, 8, MetadataSourceName.AppleBooks));
        await SaveRunAsync(dbContext, Night, discrepancies: [DateDisagreement(series)]);
        await SaveRunAsync(dbContext, Night.AddDays(1));

        var conflicts = await new SourceConflictQueryService(dbContext).GetAsync(series.Id, CancellationToken.None);

        var conflict = conflicts.ShouldNotBeNull().ShouldHaveSingleItem();
        conflict.Reason.ShouldBe(SourceDisplayReason.YourChoice);
        conflict.RecordedAt.ShouldBe(Night);
    }

    // The chapter map is checked weekly; the nightly sweeps in between do not look again, and
    // what the last check found must not vanish with them. A later check that found nothing
    // does clear it.
    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    public async Task GetAsync_ChapterRangeFromAnEarlierCheck_StandsUntilTheNextCheck(bool checkedAgain, int expected)
    {
        await using var dbContext = CreateDbContext();
        var series = await SaveSeriesAsync(dbContext);
        var checkingRun = await SaveRunAsync(dbContext, Night, discrepancies: [ChapterRangeDisagreement(series)]);
        var laterRun = await SaveRunAsync(dbContext, Night.AddDays(1));
        series.ChapterMapCheckedAt = checkedAgain ? Night.AddDays(1).AddMinutes(5) : Night.AddMinutes(5);
        series.ChapterMapSyncRunId = checkedAgain ? laterRun.Id : checkingRun.Id;
        await dbContext.SaveChangesAsync();

        var conflicts = await new SourceConflictQueryService(dbContext).GetAsync(series.Id, CancellationToken.None);

        conflicts.ShouldNotBeNull().Count.ShouldBe(expected);
        if (expected > 0)
        {
            var conflict = conflicts[0];
            conflict.Field.ShouldBe(SyncDiscrepancyField.ChapterRange);
            conflict.CanChoose.ShouldBeFalse();
            conflict.NeedsReview.ShouldBeFalse();
            conflict.RecordedAt.ShouldBe(Night);
        }
    }

    // A one-series refresh that overlaps the sweep which checked the map, and a later sweep that
    // did not look: the rows come from the run the series names, not from whichever run's
    // window happens to contain the check's time.
    [Fact]
    public async Task GetAsync_ChapterRangeCheckedDuringOverlappingRuns_ReadsTheRunThatChecked()
    {
        await using var dbContext = CreateDbContext();
        var series = await SaveSeriesAsync(dbContext);
        var checkingSweep = await SaveRunAsync(dbContext, Night, discrepancies: [ChapterRangeDisagreement(series)]);
        await SaveRunAsync(dbContext, Night.AddMinutes(10), coversWholeShelf: false);
        await SaveRunAsync(dbContext, Night.AddDays(1));
        series.ChapterMapCheckedAt = Night.AddMinutes(12);
        series.ChapterMapSyncRunId = checkingSweep.Id;
        await dbContext.SaveChangesAsync();

        var conflicts = await new SourceConflictQueryService(dbContext).GetAsync(series.Id, CancellationToken.None);

        var conflict = conflicts.ShouldNotBeNull().ShouldHaveSingleItem();
        conflict.Field.ShouldBe(SyncDiscrepancyField.ChapterRange);
        conflict.RecordedAt.ShouldBe(Night);
    }

    private static SyncDiscrepancy ChapterRangeDisagreement(Series series) =>
        SyncDiscrepancies.ChapterRange(
            series,
            new ChapterStartDisagreement(
                24,
                [
                    new SourcedVolumeRange(MetadataSourceName.Wikipedia, new VolumeChapterRange(24, 223m, null)),
                    new SourcedVolumeRange(MetadataSourceName.MangaDex, new VolumeChapterRange(24, 230m, null)),
                ],
                MetadataSourceName.Wikipedia));

    // Both per-volume passes ask about a volume that is in print with no date, and each one
    // records its own row when it dates it in the future. The page has to offer both.
    [Fact]
    public async Task GetAsync_StoreAndLibraryBothContradictTheCountInOneRun_ListsOneConflictWithBothSources()
    {
        await using var dbContext = CreateDbContext();
        var series = CreateSeries();
        var refreshed = await RefreshAsync(
            series, Info(), Catalogue(AppleRecord(FirstDate)), LibraryEdition(LibraryDate));
        refreshed.Discrepancies.Count(row => row is { Field: SyncDiscrepancyField.ReleaseDate, VolumeNumber: 8 })
            .ShouldBe(2);
        await SaveSeriesAsync(dbContext, series);
        await SaveRunAsync(dbContext, Night, discrepancies: [.. refreshed.Discrepancies]);

        var conflicts = await new SourceConflictQueryService(dbContext).GetAsync(series.Id, CancellationToken.None);

        var conflict = conflicts.ShouldNotBeNull().ShouldHaveSingleItem();
        conflict.VolumeNumber.ShouldBe(8);
        conflict.Options.Select(option => option.Source).ShouldBe(
        [
            MetadataSourceName.MangaUpdates,
            MetadataSourceName.AppleBooks,
            MetadataSourceName.OpenLibrary,
        ], ignoreOrder: true);
        conflict.Options.Single(option => option.Source is MetadataSourceName.OpenLibrary)
            .DisplayValue.ShouldBe("1 Apr 2099");
    }

    // The same merge on the path a choice takes when the latest sweep has no row for it.
    [Fact]
    public async Task GetAsync_ChoiceWithTwoRowsInAnOlderRun_ListsBothSources()
    {
        await using var dbContext = CreateDbContext();
        var series = await SaveSeriesAsync(dbContext);
        series.SourceChoices.Add(Choice(SyncDiscrepancyField.ReleaseDate, 8, MetadataSourceName.OpenLibrary));
        await SaveRunAsync(
            dbContext, Night, discrepancies: [DateDisagreement(series), LibraryDateDisagreement(series)]);
        await SaveRunAsync(dbContext, Night.AddDays(1));

        var conflicts = await new SourceConflictQueryService(dbContext).GetAsync(series.Id, CancellationToken.None);

        var conflict = conflicts.ShouldNotBeNull().ShouldHaveSingleItem();
        conflict.Options.Count.ShouldBe(3);
        conflict.DisplayedSource.ShouldBe(MetadataSourceName.OpenLibrary);
    }

    [Fact]
    public async Task GetAsync_ASettledVolumeCount_IsListedAsAutomatic()
    {
        await using var dbContext = CreateDbContext();
        var series = await SaveSeriesAsync(dbContext);
        await SaveRunAsync(dbContext, Night, discrepancies: [CountDisagreement(series)]);

        var conflicts = await new SourceConflictQueryService(dbContext).GetAsync(series.Id, CancellationToken.None);

        // Without this a volume-count choice could never be reached: counting fewer is always
        // settled automatically.
        var conflict = conflicts.ShouldNotBeNull().ShouldHaveSingleItem();
        conflict.Reason.ShouldBe(SourceDisplayReason.Automatic);
        conflict.NeedsReview.ShouldBeFalse();
        conflict.DisplayedSource.ShouldBe(MetadataSourceName.AniList);
        conflict.Explanation.ShouldContain("16");
    }

    [Fact]
    public async Task GetAsync_SeveralDisagreements_AreOrderedStatusCountThenVolumes()
    {
        await using var dbContext = CreateDbContext();
        var series = await SaveSeriesAsync(dbContext);
        await SaveRunAsync(
            dbContext,
            Night,
            discrepancies: [DateDisagreement(series), CountDisagreement(series), StatusDisagreement(series)]);

        var conflicts = await new SourceConflictQueryService(dbContext).GetAsync(series.Id, CancellationToken.None);

        conflicts.ShouldNotBeNull().Select(conflict => conflict.Field).ShouldBe(
        [
            SyncDiscrepancyField.Status,
            SyncDiscrepancyField.VolumeCount,
            SyncDiscrepancyField.ReleaseDate,
        ]);
    }

    [Fact]
    public async Task GetAsync_AResetChoice_ReadsUnresolvedAgain()
    {
        await using var dbContext = CreateDbContext();
        var series = await SaveSeriesAsync(dbContext);

        // Recorded as the user's pick; the pick has since been reset, so no choice row exists.
        await SaveRunAsync(
            dbContext, Night, discrepancies: [DateDisagreement(series, MetadataSourceName.AppleBooks)]);

        var conflicts = await new SourceConflictQueryService(dbContext).GetAsync(series.Id, CancellationToken.None);

        var conflict = conflicts.ShouldNotBeNull().ShouldHaveSingleItem();
        conflict.Reason.ShouldBe(SourceDisplayReason.WaitingOnYou);
        conflict.NeedsReview.ShouldBeTrue();
    }

    [Fact]
    public async Task GetAsync_SeriesLink_CannotBeChosen()
    {
        await using var dbContext = CreateDbContext();
        var series = await SaveSeriesAsync(dbContext);
        var link = new SyncDiscrepancy
        {
            Id = Guid.CreateVersion7(),
            SeriesId = series.Id,
            SeriesTitle = series.TitleEnglish,
            Field = SyncDiscrepancyField.SeriesLink,
        }
            .Says(MetadataSourceName.AniList, series.TitleEnglish)
            .Says(MetadataSourceName.MangaUpdates, "best match scored 61");
        await SaveRunAsync(dbContext, Night, discrepancies: [link]);

        var conflicts = await new SourceConflictQueryService(dbContext).GetAsync(series.Id, CancellationToken.None);

        var conflict = conflicts.ShouldNotBeNull().ShouldHaveSingleItem();
        conflict.CanChoose.ShouldBeFalse();
        conflict.NeedsReview.ShouldBeTrue();
        conflict.DisplayedSource.ShouldBeNull();
    }

    [Theory]
    [InlineData(SyncDiscrepancyField.ReleaseDate, "2099-03-09", "9 Mar 2099")]
    [InlineData(SyncDiscrepancyField.ReleaseDate, "in print", "in print")]
    [InlineData(SyncDiscrepancyField.VolumeCount, "18", "18 volumes")]
    [InlineData(SyncDiscrepancyField.VolumeCount, "1", "1 volume")]
    [InlineData(SyncDiscrepancyField.Status, "Hiatus", "Hiatus")]
    [InlineData(SyncDiscrepancyField.Status, "not completed", "not finished")]
    [InlineData(SyncDiscrepancyField.Status, null, "nothing")]
    public async Task GetAsync_FormatsDatesCountsAndStatuses(
        SyncDiscrepancyField field, string? value, string expected)
    {
        await using var dbContext = CreateDbContext();
        var series = await SaveSeriesAsync(dbContext);
        var row = new SyncDiscrepancy
        {
            Id = Guid.CreateVersion7(),
            SeriesId = series.Id,
            SeriesTitle = series.TitleEnglish,
            Field = field,
            VolumeNumber = field is SyncDiscrepancyField.ReleaseDate ? 8 : null,
        }
            .Says(MetadataSourceName.AniList, "Completed")
            .Says(MetadataSourceName.MangaUpdates, value);
        await SaveRunAsync(dbContext, Night, discrepancies: [row]);

        var conflicts = await new SourceConflictQueryService(dbContext).GetAsync(series.Id, CancellationToken.None);

        conflicts.ShouldNotBeNull().ShouldHaveSingleItem()
            .Options.Single(option => option.Source is MetadataSourceName.MangaUpdates)
            .DisplayValue.ShouldBe(expected);
    }

    // The discrepancy table has no owner filter of its own, so the only thing keeping another
    // user's rows out is reading them through the filtered runs.
    [Fact]
    public async Task GetAsync_AnotherUsersDiscrepancies_AreNotReturned()
    {
        var databaseName = $"source-choice-{Guid.CreateVersion7()}";
        await using var dbContext = CreateDbContext(databaseName, TestUsers.Current);
        var series = await SaveSeriesAsync(dbContext);

        await using (var otherUser = CreateDbContext(databaseName, TestUsers.As(TestUsers.SomeoneElse)))
        {
            await SaveRunAsync(otherUser, Night, discrepancies: [DateDisagreement(series)]);
        }

        var conflicts = await new SourceConflictQueryService(dbContext).GetAsync(series.Id, CancellationToken.None);

        conflicts.ShouldNotBeNull().ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAsync_AnUnknownSeries_ReturnsNull()
    {
        await using var dbContext = CreateDbContext();

        var conflicts = await new SourceConflictQueryService(dbContext)
            .GetAsync(Guid.CreateVersion7(), CancellationToken.None);

        conflicts.ShouldBeNull();
    }
}
