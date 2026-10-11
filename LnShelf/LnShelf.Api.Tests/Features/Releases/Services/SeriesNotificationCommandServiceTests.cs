using LnShelf.Api.Features.Releases.Services;
using Microsoft.EntityFrameworkCore;

namespace LnShelf.Api.Tests.Features.Releases.Services;

/// <summary>The per-series mute: written to the user-state row, answered with the whole series.</summary>
public sealed class SeriesNotificationCommandServiceTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SetMutedAsync_ASeriesNeverTouched_CreatesItsStateWithTheFlag(bool muted)
    {
        await using var dbContext = CreateDbContext();
        var series = new Series { Id = Guid.CreateVersion7(), TitleEnglish = "Overlord" };
        dbContext.Series.Add(series);
        await dbContext.SaveChangesAsync();

        var detail = await CreateService(dbContext).SetMutedAsync(series.Id, muted, CancellationToken.None);

        detail.ShouldNotBeNull().NotificationsMuted.ShouldBe(muted);
        (await dbContext.SeriesUserStates.SingleAsync()).NotificationsMuted.ShouldBe(muted);
    }

    [Fact]
    public async Task SetMutedAsync_UnmutingAMutedSeries_KeepsTheRestOfItsState()
    {
        await using var dbContext = CreateDbContext();
        var series = new Series { Id = Guid.CreateVersion7(), TitleEnglish = "Overlord" };
        series.UserState = new SeriesUserState
        {
            SeriesId = series.Id,
            TrackingStatus = TrackingStatus.Reading,
            Notes = "Waiting for 18",
            NotificationsMuted = true,
        };
        dbContext.Series.Add(series);
        await dbContext.SaveChangesAsync();

        var detail = await CreateService(dbContext).SetMutedAsync(series.Id, muted: false, CancellationToken.None);

        detail.ShouldNotBeNull().NotificationsMuted.ShouldBeFalse();
        detail.TrackingStatus.ShouldBe(TrackingStatus.Reading);
        detail.Notes.ShouldBe("Waiting for 18");
    }

    [Fact]
    public async Task SetMutedAsync_NoSuchSeries_IsNull()
    {
        await using var dbContext = CreateDbContext();

        (await CreateService(dbContext).SetMutedAsync(Guid.CreateVersion7(), muted: true, CancellationToken.None))
            .ShouldBeNull();
    }

    private static SeriesNotificationCommandService CreateService(LnShelfDbContext dbContext) =>
        new(dbContext, TimeProvider.System, NullLogger<SeriesNotificationCommandService>.Instance);

    private static LnShelfDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<LnShelfDbContext>()
            .UseInMemoryDatabase($"series-notifications-{Guid.CreateVersion7()}")
            .Options, TestUsers.Current);
}
