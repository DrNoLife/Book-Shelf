using LnShelf.Api.Extensions;
using Microsoft.EntityFrameworkCore;

namespace LnShelf.Api.Tests.Features.Shelf.Services;

/// <summary>
/// Chapter progress: the per-series unit, the last chapter read, what is refused, and the one
/// status move a chapter is allowed to make.
/// </summary>
public sealed class SeriesProgressCommandServiceTests
{
    [Theory]
    [InlineData("142")]
    [InlineData("10.5")]
    [InlineData("0")]
    [InlineData("100000")]
    public async Task SetLastChapterReadAsync_AMangaSeries_StoresTheChapterAndAnswersWithTheSeries(string text)
    {
        var chapter = decimal.Parse(text, CultureInfo.InvariantCulture);
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, Format.Manga, TrackingStatus.Reading);

        var detail = await CreateService(dbContext).SetLastChapterReadAsync(
            series.Id, new SetLastChapterReadRequest { LastChapterRead = chapter }, CancellationToken.None);

        detail.ShouldNotBeNull().LastChapterRead.ShouldBe(chapter);
        (await dbContext.SeriesUserStates.SingleAsync()).LastChapterRead.ShouldBe(chapter);
    }

    [Fact]
    public async Task SetLastChapterReadAsync_ANull_ClearsTheChapter()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, Format.Manga, TrackingStatus.Reading, lastChapterRead: 142m);

        var detail = await CreateService(dbContext).SetLastChapterReadAsync(
            series.Id, new SetLastChapterReadRequest { LastChapterRead = null }, CancellationToken.None);

        detail.ShouldNotBeNull().LastChapterRead.ShouldBeNull();
        (await dbContext.SeriesUserStates.SingleAsync()).LastChapterRead.ShouldBeNull();
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("100000.01")]
    [InlineData("10.555")]
    public async Task SetLastChapterReadAsync_ANumberOutOfRange_IsRefusedAndNothingIsWritten(string text)
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, Format.Manga, TrackingStatus.Reading, lastChapterRead: 142m);

        var act = () => CreateService(dbContext).SetLastChapterReadAsync(
            series.Id,
            new SetLastChapterReadRequest { LastChapterRead = decimal.Parse(text, CultureInfo.InvariantCulture) },
            CancellationToken.None);

        await act.ShouldThrowAsync<InvalidInputException>();
        (await dbContext.SeriesUserStates.SingleAsync()).LastChapterRead.ShouldBe(142m);
    }

    [Fact]
    public async Task SetLastChapterReadAsync_ALightNovel_IsRefused()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, Format.LightNovel, TrackingStatus.Reading);

        var act = () => CreateService(dbContext).SetLastChapterReadAsync(
            series.Id, new SetLastChapterReadRequest { LastChapterRead = 12m }, CancellationToken.None);

        (await act.ShouldThrowAsync<InvalidInputException>()).Message.ShouldContain("light novel");
        (await dbContext.SeriesUserStates.SingleAsync()).LastChapterRead.ShouldBeNull();
    }

    [Theory]
    // starting status, chapter -> status after
    [InlineData(TrackingStatus.PlanToStart, "1", TrackingStatus.Reading)]
    [InlineData(TrackingStatus.PlanToStart, "0.5", TrackingStatus.Reading)]
    [InlineData(TrackingStatus.PlanToStart, "0", TrackingStatus.PlanToStart)]
    [InlineData(TrackingStatus.Reading, "12", TrackingStatus.Reading)]
    [InlineData(TrackingStatus.Dropped, "12", TrackingStatus.Dropped)]
    [InlineData(TrackingStatus.Completed, "12", TrackingStatus.Completed)]
    public async Task SetLastChapterReadAsync_AChapterAboveZero_MovesOnlyPlanToStartToReading(
        TrackingStatus starting, string chapter, TrackingStatus expected)
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, Format.Manga, starting);

        var detail = await CreateService(dbContext).SetLastChapterReadAsync(
            series.Id,
            new SetLastChapterReadRequest { LastChapterRead = decimal.Parse(chapter, CultureInfo.InvariantCulture) },
            CancellationToken.None);

        detail.ShouldNotBeNull().TrackingStatus.ShouldBe(expected);
    }

    // A series nothing was ever recorded on has no state row, and reads as PlanToStart; the
    // first chapter creates the row and starts it.
    [Fact]
    public async Task SetLastChapterReadAsync_ASeriesWithNoStateRow_GetsOneAndMovesToReading()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, Format.WebNovel, trackingStatus: null);

        await CreateService(dbContext).SetLastChapterReadAsync(
            series.Id, new SetLastChapterReadRequest { LastChapterRead = 3m }, CancellationToken.None);

        var state = await dbContext.SeriesUserStates.SingleAsync();
        state.LastChapterRead.ShouldBe(3m);
        state.TrackingStatus.ShouldBe(TrackingStatus.Reading);
        state.ProgressUnit.ShouldBe(ProgressUnit.Volumes);
    }

    [Fact]
    public async Task SetLastChapterReadAsync_NoSuchSeries_ReturnsNull()
    {
        await using var dbContext = CreateDbContext();

        var detail = await CreateService(dbContext).SetLastChapterReadAsync(
            Guid.NewGuid(), new SetLastChapterReadRequest { LastChapterRead = 1m }, CancellationToken.None);

        detail.ShouldBeNull();
    }

    // The default every existing row has: the unit is Volumes until the reader chooses.
    [Fact]
    public void Untouched_ANewStateRow_CountsInVolumes() =>
        SeriesUserState.Untouched(Guid.CreateVersion7()).ProgressUnit.ShouldBe(ProgressUnit.Volumes);

    [Fact]
    public async Task SetProgressUnitAsync_ToChaptersAndBack_KeepsTheChapterAndEveryVolumeState()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, Format.Manga, TrackingStatus.Reading, lastChapterRead: 142m);
        var service = CreateService(dbContext);

        var chapters = await service.SetProgressUnitAsync(
            series.Id, new SetProgressUnitRequest { ProgressUnit = ProgressUnit.Chapters }, CancellationToken.None);

        chapters.ShouldNotBeNull().ProgressUnit.ShouldBe(ProgressUnit.Chapters);
        chapters.LastChapterRead.ShouldBe(142m);
        AssertVolumesUntouched(chapters);

        var volumes = await service.SetProgressUnitAsync(
            series.Id, new SetProgressUnitRequest { ProgressUnit = ProgressUnit.Volumes }, CancellationToken.None);

        // Switching back is a view of the series, not a reset: the number stays for next time.
        volumes.ShouldNotBeNull().ProgressUnit.ShouldBe(ProgressUnit.Volumes);
        volumes.LastChapterRead.ShouldBe(142m);
        AssertVolumesUntouched(volumes);

        var stored = await dbContext.SeriesUserStates.SingleAsync();
        stored.ProgressUnit.ShouldBe(ProgressUnit.Volumes);
        stored.LastChapterRead.ShouldBe(142m);
        stored.TrackingStatus.ShouldBe(TrackingStatus.Reading);
    }

    [Fact]
    public async Task SetProgressUnitAsync_ChaptersOnALightNovel_IsRefused()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, Format.LightNovel, TrackingStatus.Reading);

        var act = () => CreateService(dbContext).SetProgressUnitAsync(
            series.Id, new SetProgressUnitRequest { ProgressUnit = ProgressUnit.Chapters }, CancellationToken.None);

        await act.ShouldThrowAsync<InvalidInputException>();
        (await dbContext.SeriesUserStates.SingleAsync()).ProgressUnit.ShouldBe(ProgressUnit.Volumes);
    }

    // Volumes is what a light novel already is, so asking for it is no error.
    [Fact]
    public async Task SetProgressUnitAsync_VolumesOnALightNovel_IsAccepted()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, Format.LightNovel, TrackingStatus.Reading);

        var detail = await CreateService(dbContext).SetProgressUnitAsync(
            series.Id, new SetProgressUnitRequest { ProgressUnit = ProgressUnit.Volumes }, CancellationToken.None);

        detail.ShouldNotBeNull().ProgressUnit.ShouldBe(ProgressUnit.Volumes);
    }

    [Fact]
    public async Task SetProgressUnitAsync_ToChapters_LeavesTheTrackingStatusAlone()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, Format.Manga, TrackingStatus.PlanToStart);

        var detail = await CreateService(dbContext).SetProgressUnitAsync(
            series.Id, new SetProgressUnitRequest { ProgressUnit = ProgressUnit.Chapters }, CancellationToken.None);

        detail.ShouldNotBeNull().TrackingStatus.ShouldBe(TrackingStatus.PlanToStart);
    }

    // Continue reading orders by "last read", so a chapter reader's series has to move it.
    [Fact]
    public async Task SetLastChapterReadAsync_AHigherChapter_MovesLastReadUpToToday()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(
            dbContext, Format.Manga, TrackingStatus.Reading, lastChapterRead: 142m, lastReadAt: new DateOnly(2026, 1, 1));

        await CreateService(dbContext).SetLastChapterReadAsync(
            series.Id, new SetLastChapterReadRequest { LastChapterRead = 143m }, CancellationToken.None);

        (await dbContext.SeriesUserStates.SingleAsync()).LastReadAt.ShouldBe(TimeProvider.System.Today());
    }

    // The library's Undo writes the lower chapter back; that is not a reading.
    [Theory]
    [InlineData("141")]
    [InlineData(null)]
    public async Task SetLastChapterReadAsync_ALowerChapterOrNone_LeavesLastReadAlone(string? text)
    {
        var lastReadAt = new DateOnly(2026, 1, 1);
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(
            dbContext, Format.Manga, TrackingStatus.Reading, lastChapterRead: 142m, lastReadAt: lastReadAt);

        await CreateService(dbContext).SetLastChapterReadAsync(
            series.Id,
            new SetLastChapterReadRequest { LastChapterRead = text is null ? null : decimal.Parse(text, CultureInfo.InvariantCulture) },
            CancellationToken.None);

        (await dbContext.SeriesUserStates.SingleAsync()).LastReadAt.ShouldBe(lastReadAt);
    }

    private static void AssertVolumesUntouched(SeriesDetailResponse detail)
    {
        detail.Volumes.Select(volume => (volume.ReadStatus, volume.Owned))
            .ShouldBe([(ReadStatus.Read, true), (ReadStatus.Unread, true), (ReadStatus.Unread, false)]);
    }

    private static SeriesProgressCommandService CreateService(LnShelfDbContext dbContext) =>
        new(dbContext, TimeProvider.System, NullLogger<SeriesProgressCommandService>.Instance);

    /// <param name="trackingStatus">Null seeds no user-state row at all.</param>
    private static async Task<Series> SeedAsync(
        LnShelfDbContext dbContext,
        Format format,
        TrackingStatus? trackingStatus,
        decimal? lastChapterRead = null,
        DateOnly? lastReadAt = null)
    {
        var series = new Series
        {
            Id = Guid.CreateVersion7(),
            MangaUpdatesId = 75336092483,
            TitleEnglish = "Chainsaw Man",
            Format = format,
            PublicationStatus = PublicationStatus.Ongoing,
            LatestChapter = 232m,
            UserState = trackingStatus is { } status
                ? new SeriesUserState { TrackingStatus = status, LastChapterRead = lastChapterRead, LastReadAt = lastReadAt }
                : null,
        };

        // Vol 1 read and owned, Vol 2 owned, Vol 3 out and not owned: one of each, so a write
        // that touched any volume state would show.
        (ReadStatus Read, bool Owned)[] states = [(ReadStatus.Read, true), (ReadStatus.Unread, true), (ReadStatus.Unread, false)];
        for (var index = 0; index < states.Length; index++)
        {
            var volume = new Volume { Id = Guid.CreateVersion7(), Number = index + 1, IsReleased = true };
            volume.UserState = new VolumeUserState
            {
                VolumeId = volume.Id,
                ReadStatus = states[index].Read,
                Owned = states[index].Owned,
            };
            series.Volumes.Add(volume);
        }

        dbContext.Series.Add(series);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        return series;
    }

    private static LnShelfDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<LnShelfDbContext>()
            .UseInMemoryDatabase($"series-progress-{Guid.CreateVersion7()}")
            .Options, TestUsers.Current);
}
