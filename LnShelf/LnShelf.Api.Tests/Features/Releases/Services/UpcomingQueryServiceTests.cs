using LnShelf.Api.Data;
using LnShelf.Api.Features.Releases.Services;
using Microsoft.EntityFrameworkCore;
using static LnShelf.Api.Tests.Features.Releases.Fixtures.UpcomingShelf;

namespace LnShelf.Api.Tests.Features.Releases.Services;

/// <summary>
/// What the query hands the grouping: which series are in scope at all.
/// </summary>
public sealed class UpcomingQueryServiceTests
{
    [Fact]
    public async Task GetUpcomingAsync_ADroppedSeries_IsLeftOut()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Series.Add(CreateSeries("Overlord", trackingStatus: TrackingStatus.Dropped, volumes: [InPrint(1)]));
        dbContext.Series.Add(CreateSeries("86—EIGHTY-SIX", volumes: [InPrint(1)]));
        await dbContext.SaveChangesAsync();

        var upcoming = await CreateService(dbContext).GetUpcomingAsync(format: null, CancellationToken.None);

        upcoming.OutNow.ShouldHaveSingleItem().SeriesTitle.ShouldBe("86—EIGHTY-SIX");
    }

    [Fact]
    public async Task GetUpcomingAsync_ASeriesWithNoUserState_IsKept()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Series.Add(CreateSeries("Overlord", trackingStatus: null, volumes: [InPrint(1)]));
        await dbContext.SaveChangesAsync();

        var upcoming = await CreateService(dbContext).GetUpcomingAsync(format: null, CancellationToken.None);

        upcoming.OutNow.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task GetUpcomingAsync_AFormat_NarrowsToThatFormat()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Series.Add(CreateSeries("That Time I Got Reincarnated as a Slime", Format.LightNovel, volumes: [InPrint(1)]));
        dbContext.Series.Add(CreateSeries("That Time I Got Reincarnated as a Slime", Format.Manga, volumes: [InPrint(1)]));
        await dbContext.SaveChangesAsync();

        var upcoming = await CreateService(dbContext).GetUpcomingAsync(Format.Manga, CancellationToken.None);

        upcoming.OutNow.ShouldHaveSingleItem().Format.ShouldBe(Format.Manga);
    }

    private static UpcomingQueryService CreateService(LnShelfDbContext dbContext) =>
        new(dbContext, TimeProvider.System);

    private static LnShelfDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<LnShelfDbContext>()
            .UseInMemoryDatabase($"upcoming-{Guid.CreateVersion7()}")
            .Options);
}
