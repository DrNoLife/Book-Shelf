using static LnShelf.Api.Tests.Features.Import.Fixtures.ImportShelfWriterHarness;

namespace LnShelf.Api.Tests.Features.Import.Services;

/// <summary>
/// The one rule the import applies to dates: the earlier of what is on the shelf and
/// what the export says survives, and null never wins.
/// </summary>
// Its own class rather than more cases in ImportShelfWriterTests, because it is the one
// place the importer is allowed to replace a value already on the shelf and the reason
// it may is worth stating in one place: the value it replaces was stamped by the app,
// not typed by the user.
public sealed class ImportShelfWriterDateTests
{
    private static readonly DateOnly ExportDate = new(2021, 11, 23);
    private static readonly DateOnly StampedToday = new(2026, 9, 6);

    [Fact]
    public async Task ApplyAsync_AVolumeStampedReadToday_TakesTheExportsEarlierDate()
    {
        await using var dbContext = CreateDbContext();
        var writer = CreateWriter(dbContext, CreateMetadataSource(volumeCount: 18));

        // How a date gets onto a volume without an import: the user ticks it read and
        // VolumeCommandService stamps today. "Today" is when the box was ticked, not
        // when the book was read.
        await writer.ApplyAsync(
            AniListId,
            [CreateGroup([1], readAt: StampedToday)],
            new ImportWriteCounters(),
            AppliedAt,
            CancellationToken.None);
        await dbContext.SaveChangesAsync();

        await writer.ApplyAsync(
            AniListId,
            [CreateGroup([1], readAt: ExportDate)],
            new ImportWriteCounters(),
            AppliedAt,
            CancellationToken.None);
        await dbContext.SaveChangesAsync();

        (await LoadVolumeAsync(dbContext, 1)).UserState!.ReadAt.ShouldBe(ExportDate);
    }

    [Fact]
    public async Task ApplyAsync_AVolumeWithAnEarlierDate_KeepsIt()
    {
        await using var dbContext = CreateDbContext();
        var writer = CreateWriter(dbContext, CreateMetadataSource(volumeCount: 18));

        await writer.ApplyAsync(
            AniListId,
            [CreateGroup([1], readAt: ExportDate)],
            new ImportWriteCounters(),
            AppliedAt,
            CancellationToken.None);
        await dbContext.SaveChangesAsync();

        await writer.ApplyAsync(
            AniListId,
            [CreateGroup([1], readAt: StampedToday)],
            new ImportWriteCounters(),
            AppliedAt,
            CancellationToken.None);
        await dbContext.SaveChangesAsync();

        (await LoadVolumeAsync(dbContext, 1)).UserState!.ReadAt.ShouldBe(ExportDate);
    }

    [Fact]
    public async Task ApplyAsync_AnImportWithNoDate_LeavesTheExistingOneAlone()
    {
        await using var dbContext = CreateDbContext();
        var writer = CreateWriter(dbContext, CreateMetadataSource(volumeCount: 18));

        await writer.ApplyAsync(
            AniListId,
            [CreateGroup([1], readAt: ExportDate)],
            new ImportWriteCounters(),
            AppliedAt,
            CancellationToken.None);
        await dbContext.SaveChangesAsync();

        // 24 of the real export's 76 rows are marked read with no date at all, so this
        // is the common case, not an edge one: an import never writes null over a date.
        var undated = CreateGroup([1]);
        undated.Rows.Single().ReadAt = null;

        await writer.ApplyAsync(
            AniListId, [undated], new ImportWriteCounters(), AppliedAt, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        (await LoadVolumeAsync(dbContext, 1)).UserState!.ReadAt.ShouldBe(ExportDate);
    }

    [Fact]
    public async Task ApplyAsync_AnUndatedVolume_TakesTheImportsDate()
    {
        await using var dbContext = CreateDbContext();
        var writer = CreateWriter(dbContext, CreateMetadataSource(volumeCount: 18));

        var undated = CreateGroup([1]);
        undated.Rows.Single().ReadAt = null;

        await writer.ApplyAsync(
            AniListId, [undated], new ImportWriteCounters(), AppliedAt, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        await writer.ApplyAsync(
            AniListId,
            [CreateGroup([1], readAt: ExportDate)],
            new ImportWriteCounters(),
            AppliedAt,
            CancellationToken.None);
        await dbContext.SaveChangesAsync();

        (await LoadVolumeAsync(dbContext, 1)).UserState!.ReadAt.ShouldBe(ExportDate);
    }

    [Fact]
    public async Task ApplyAsync_ASeriesStartedLaterThanTheExportSays_MovesStartedAtBack()
    {
        await using var dbContext = CreateDbContext();
        var writer = CreateWriter(dbContext, CreateMetadataSource(volumeCount: 18));

        await writer.ApplyAsync(
            AniListId,
            [CreateGroup([1], readAt: StampedToday)],
            new ImportWriteCounters(),
            AppliedAt,
            CancellationToken.None);
        await dbContext.SaveChangesAsync();

        (await LoadSeriesAsync(dbContext)).UserState!.StartedAt.ShouldBe(StampedToday);

        // An older export imported after a newer one. Without this the start date could
        // never move back to where the reading actually began.
        await writer.ApplyAsync(
            AniListId,
            [CreateGroup([1], readAt: ExportDate)],
            new ImportWriteCounters(),
            AppliedAt,
            CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var userState = (await LoadSeriesAsync(dbContext)).UserState!;
        userState.StartedAt.ShouldBe(ExportDate);

        // The other end of the series' reading still tracks the newest date read.
        userState.LastReadAt.ShouldBe(StampedToday);
    }

    [Fact]
    public async Task ApplyAsync_AnImportWithNoDatesAtAll_LeavesStartedAtAlone()
    {
        await using var dbContext = CreateDbContext();
        var writer = CreateWriter(dbContext, CreateMetadataSource(volumeCount: 18));

        await writer.ApplyAsync(
            AniListId,
            [CreateGroup([1], readAt: ExportDate)],
            new ImportWriteCounters(),
            AppliedAt,
            CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var undated = CreateGroup([2]);
        undated.Rows.Single().ReadAt = null;

        await writer.ApplyAsync(
            AniListId, [undated], new ImportWriteCounters(), AppliedAt, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        (await LoadSeriesAsync(dbContext)).UserState!.StartedAt.ShouldBe(ExportDate);
    }

    [Theory]
    [InlineData(ReadStatus.Reading)]
    [InlineData(ReadStatus.Unread)]
    public async Task ApplyAsync_ADatedRowThatIsNotRead_GivesTheVolumeNoReadDate(ReadStatus readStatus)
    {
        // Did-not-finish maps to Unread and currently-reading to Reading; both exports put
        // a date on those rows too.
        await using var dbContext = CreateDbContext();
        var writer = CreateWriter(dbContext, CreateMetadataSource(volumeCount: 18));

        await writer.ApplyAsync(
            AniListId,
            [CreateGroup([1], readStatus, readAt: ExportDate)],
            new ImportWriteCounters(),
            AppliedAt,
            CancellationToken.None);
        await dbContext.SaveChangesAsync();

        (await LoadVolumeAsync(dbContext, 1)).UserState!.ReadAt.ShouldBeNull();
        (await LoadSeriesAsync(dbContext)).UserState!.LastReadAt.ShouldBeNull();
    }

    [Fact]
    public async Task ApplyAsync_ADatedCurrentlyReadingRow_StillDatesTheStartOfTheSeries()
    {
        await using var dbContext = CreateDbContext();
        var writer = CreateWriter(dbContext, CreateMetadataSource(volumeCount: 18));

        // The date on a currently-reading row is an earlier finished read: proof the
        // reading had begun by then, though not that the volume is read now.
        await writer.ApplyAsync(
            AniListId,
            [CreateGroup([1], ReadStatus.Reading, readAt: ExportDate), CreateGroup([2], readAt: StampedToday)],
            new ImportWriteCounters(),
            AppliedAt,
            CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var userState = (await LoadSeriesAsync(dbContext)).UserState!;
        userState.StartedAt.ShouldBe(ExportDate);
        userState.LastReadAt.ShouldBe(StampedToday);
    }

    [Fact]
    public async Task ApplyAsync_ADatedDidNotFinishRow_DatesNothing()
    {
        await using var dbContext = CreateDbContext();
        var writer = CreateWriter(dbContext, CreateMetadataSource(volumeCount: 18));

        await writer.ApplyAsync(
            AniListId,
            [CreateGroup([1], ReadStatus.Unread, readAt: ExportDate)],
            new ImportWriteCounters(),
            AppliedAt,
            CancellationToken.None);
        await dbContext.SaveChangesAsync();

        (await LoadSeriesAsync(dbContext)).UserState!.StartedAt.ShouldBeNull();
    }

    [Fact]
    public async Task ApplyAsync_TwoRowsForOneVolume_KeepTheLaterReadDate()
    {
        await using var dbContext = CreateDbContext();
        var writer = CreateWriter(dbContext, CreateMetadataSource(volumeCount: 18));

        // The two-level rule's other half. Within one import, two rows for one volume
        // are two readings of it and the shelf shows the newest; only against a date
        // already on the shelf does the earlier win.
        await writer.ApplyAsync(
            AniListId,
            [CreateGroup([1], readAt: ExportDate), CreateGroup([1], readAt: StampedToday)],
            new ImportWriteCounters(),
            AppliedAt,
            CancellationToken.None);
        await dbContext.SaveChangesAsync();

        (await LoadVolumeAsync(dbContext, 1)).UserState!.ReadAt.ShouldBe(StampedToday);
    }
}
