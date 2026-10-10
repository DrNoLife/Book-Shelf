using Microsoft.EntityFrameworkCore;

namespace LnShelf.Api.Tests.Features.Shelf.Services;

/// <summary>
/// Correcting when a read volume was read: the single edit, the clear, and the bulk
/// variant with its all-or-nothing rule for volumes that are not read.
/// </summary>
public sealed class VolumeReadDateCommandServiceTests
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Now.Date);

    [Fact]
    public async Task SetAsync_AReadVolume_StoresTheDateAndAnswersWithTheSeries()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, [ReadStatus.Read, ReadStatus.Read]);
        var volume = series.Volumes.Single(candidate => candidate.Number == 1);

        var detail = await CreateService(dbContext).SetAsync(
            volume.Id,
            new SetVolumeReadDateRequest { ReadAt = new DateOnly(2024, 3, 14) },
            CancellationToken.None);

        detail.ShouldNotBeNull().Volumes.Single(row => row.VolumeId == volume.Id).ReadAt
            .ShouldBe(new DateOnly(2024, 3, 14));
        (await dbContext.VolumeUserStates.SingleAsync(state => state.VolumeId == volume.Id)).ReadAt
            .ShouldBe(new DateOnly(2024, 3, 14));
    }

    [Fact]
    public async Task SetAsync_ANullDate_ClearsItAndLeavesTheVolumeRead()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, [ReadStatus.Read]);
        var volume = series.Volumes.Single();

        await CreateService(dbContext).SetAsync(
            volume.Id, new SetVolumeReadDateRequest { ReadAt = null }, CancellationToken.None);

        var state = await dbContext.VolumeUserStates.SingleAsync();
        state.ReadAt.ShouldBeNull();
        state.ReadStatus.ShouldBe(ReadStatus.Read);
    }

    [Fact]
    public async Task SetAsync_TodayAndTheFirstAcceptedDay_AreBothAllowed()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, [ReadStatus.Read]);
        var service = CreateService(dbContext);

        await service.SetAsync(
            series.Volumes.Single().Id, new SetVolumeReadDateRequest { ReadAt = Today }, CancellationToken.None);
        await service.SetAsync(
            series.Volumes.Single().Id,
            new SetVolumeReadDateRequest { ReadAt = new DateOnly(1900, 1, 1) },
            CancellationToken.None);

        (await dbContext.VolumeUserStates.SingleAsync()).ReadAt.ShouldBe(new DateOnly(1900, 1, 1));
    }

    [Theory]
    [InlineData(ReadStatus.Unread)]
    [InlineData(ReadStatus.Reading)]
    public async Task SetAsync_AVolumeThatIsNotRead_IsRefused(ReadStatus status)
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, [status]);

        var act = async () => await CreateService(dbContext).SetAsync(
            series.Volumes.Single().Id,
            new SetVolumeReadDateRequest { ReadAt = new DateOnly(2024, 3, 14) },
            CancellationToken.None);

        await act.ShouldThrowAsync<InvalidInputException>();
        (await dbContext.VolumeUserStates.SingleAsync()).ReadAt.ShouldBeNull();
    }

    [Fact]
    public async Task SetAsync_AVolumeWithNoStateRow_IsRefused()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, [null]);

        var act = async () => await CreateService(dbContext).SetAsync(
            series.Volumes.Single().Id,
            new SetVolumeReadDateRequest { ReadAt = Today },
            CancellationToken.None);

        await act.ShouldThrowAsync<InvalidInputException>();
    }

    [Fact]
    public async Task SetAsync_TomorrowOrBefore1900_IsRefused()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, [ReadStatus.Read]);
        var service = CreateService(dbContext);
        var volumeId = series.Volumes.Single().Id;

        await Should.ThrowAsync<InvalidInputException>(() => service.SetAsync(
            volumeId, new SetVolumeReadDateRequest { ReadAt = Today.AddDays(1) }, CancellationToken.None));
        await Should.ThrowAsync<InvalidInputException>(() => service.SetAsync(
            volumeId, new SetVolumeReadDateRequest { ReadAt = new DateOnly(1899, 12, 31) }, CancellationToken.None));
    }

    [Fact]
    public async Task SetAsync_NoSuchVolume_ReturnsNull()
    {
        await using var dbContext = CreateDbContext();
        await SeedAsync(dbContext, [ReadStatus.Read]);

        var detail = await CreateService(dbContext).SetAsync(
            Guid.NewGuid(), new SetVolumeReadDateRequest { ReadAt = Today }, CancellationToken.None);

        detail.ShouldBeNull();
    }

    [Fact]
    public async Task SetAsync_CorrectingAnOldDate_LeavesTheSeriesLastReadDateAlone()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, [ReadStatus.Read]);
        series.UserState.ShouldNotBeNull().LastReadAt = Today;
        await dbContext.SaveChangesAsync();

        await CreateService(dbContext).SetAsync(
            series.Volumes.Single().Id,
            new SetVolumeReadDateRequest { ReadAt = new DateOnly(2023, 5, 1) },
            CancellationToken.None);

        (await dbContext.SeriesUserStates.SingleAsync()).LastReadAt.ShouldBe(Today);
    }

    [Fact]
    public async Task SetManyAsync_ReadVolumes_AllGetTheDateAndOthersAreUntouched()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, [ReadStatus.Read, ReadStatus.Read, ReadStatus.Read]);
        var chosen = series.Volumes.Where(volume => volume.Number <= 2).Select(volume => volume.Id).ToList();

        var detail = await CreateService(dbContext).SetManyAsync(
            series.Id,
            new SetVolumeReadDatesRequest { VolumeIds = chosen, ReadAt = new DateOnly(2022, 8, 20) },
            CancellationToken.None);

        detail.ShouldNotBeNull();
        detail.Volumes.Where(row => row.Number <= 2).ShouldAllBe(row => row.ReadAt == new DateOnly(2022, 8, 20));
        detail.Volumes.Single(row => row.Number == 3).ReadAt.ShouldNotBe(new DateOnly(2022, 8, 20));
    }

    /// <summary>
    /// The documented choice: a selection with an unread volume in it is refused whole,
    /// because a partial write the user did not see would be the worse surprise. The
    /// client sends only volumes it can see are read.
    /// </summary>
    [Fact]
    public async Task SetManyAsync_WithAnUnreadVolumeAmongThem_RefusesAndWritesNothing()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, [ReadStatus.Read, ReadStatus.Unread, ReadStatus.Read]);

        var act = async () => await CreateService(dbContext).SetManyAsync(
            series.Id,
            new SetVolumeReadDatesRequest
            {
                VolumeIds = series.Volumes.Select(volume => volume.Id).ToList(),
                ReadAt = new DateOnly(2022, 8, 20),
            },
            CancellationToken.None);

        await act.ShouldThrowAsync<InvalidInputException>();
        (await dbContext.VolumeUserStates.Where(state => state.ReadAt == new DateOnly(2022, 8, 20)).CountAsync())
            .ShouldBe(0);
    }

    [Fact]
    public async Task SetManyAsync_AVolumeFromAnotherSeries_IsRefused()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, [ReadStatus.Read]);

        var act = async () => await CreateService(dbContext).SetManyAsync(
            series.Id,
            new SetVolumeReadDatesRequest { VolumeIds = [Guid.NewGuid()], ReadAt = Today },
            CancellationToken.None);

        await act.ShouldThrowAsync<InvalidInputException>();
    }

    [Fact]
    public async Task SetManyAsync_NoVolumesOrAFutureDate_IsRefused()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, [ReadStatus.Read]);
        var service = CreateService(dbContext);

        await Should.ThrowAsync<InvalidInputException>(() => service.SetManyAsync(
            series.Id, new SetVolumeReadDatesRequest { VolumeIds = [], ReadAt = Today }, CancellationToken.None));
        await Should.ThrowAsync<InvalidInputException>(() => service.SetManyAsync(
            series.Id,
            new SetVolumeReadDatesRequest
            {
                VolumeIds = [series.Volumes.Single().Id],
                ReadAt = Today.AddDays(1),
            },
            CancellationToken.None));
    }

    [Fact]
    public async Task SetManyAsync_NoSuchSeries_ReturnsNull()
    {
        await using var dbContext = CreateDbContext();

        var detail = await CreateService(dbContext).SetManyAsync(
            Guid.NewGuid(),
            new SetVolumeReadDatesRequest { VolumeIds = [Guid.NewGuid()], ReadAt = Today },
            CancellationToken.None);

        detail.ShouldBeNull();
    }

    private static VolumeReadDateCommandService CreateService(LnShelfDbContext dbContext) =>
        new(dbContext, TimeProvider.System, NullLogger<VolumeReadDateCommandService>.Instance);

    /// <summary>One volume per status given; a null status is a volume with no state row.</summary>
    private static async Task<Series> SeedAsync(LnShelfDbContext dbContext, IReadOnlyList<ReadStatus?> statuses)
    {
        var series = new Series
        {
            Id = Guid.CreateVersion7(),
            AniListId = 94846,
            TitleEnglish = "The Saga of Tanya the Evil",
            Format = Format.LightNovel,
            PublicationStatus = PublicationStatus.Ongoing,
            UserState = new SeriesUserState { TrackingStatus = TrackingStatus.Reading },
        };

        for (var index = 0; index < statuses.Count; index++)
        {
            var volume = new Volume { Id = Guid.CreateVersion7(), Number = index + 1, IsReleased = true };
            if (statuses[index] is { } status)
            {
                volume.UserState = new VolumeUserState
                {
                    VolumeId = volume.Id,
                    ReadStatus = status,
                    ReadAt = status is ReadStatus.Read ? Today : null,
                };
            }

            series.Volumes.Add(volume);
        }

        dbContext.Series.Add(series);
        await dbContext.SaveChangesAsync();
        return series;
    }

    private static LnShelfDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<LnShelfDbContext>()
            .UseInMemoryDatabase($"read-dates-{Guid.CreateVersion7()}")
            .Options, TestUsers.Current);
}
