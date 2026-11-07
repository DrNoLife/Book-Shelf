using LnShelf.Api.Tests.Data.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace LnShelf.Api.Tests.Features.Shelf.Services;

/// <summary>
/// One retry, and only one, when a save is refused as a unique violation.
/// </summary>
// The refusal is faked here so the retry's edges are pinned without Docker; the real race on
// Postgres is VolumeCommandServiceRaceTests.
public sealed class FirstTouchRetryTests
{
    [Fact]
    public async Task SetReadAsync_TheFirstSaveIsAUniqueViolation_RetriesAndSaves()
    {
        var refusals = 0;
        await using var dbContext = CreateDbContext(() => refusals++ < 1);
        var volumeId = await SeedVolumeAsync(dbContext);

        var detail = await CreateService(dbContext).SetReadAsync(
            volumeId, new SetVolumeReadRequest { Status = ReadStatus.Read }, CancellationToken.None);

        detail.ShouldNotBeNull().Volumes.Single().ReadStatus.ShouldBe(ReadStatus.Read);
        (await dbContext.VolumeUserStates.SingleAsync()).ReadStatus.ShouldBe(ReadStatus.Read);
    }

    [Fact]
    public async Task SetOwnedAsync_EverySaveIsAUniqueViolation_LetsTheSecondOneBubble()
    {
        var attempts = 0;
        await using var dbContext = CreateDbContext(() =>
        {
            attempts++;
            return true;
        });
        var volumeId = await SeedVolumeAsync(dbContext);

        var exception = await Should.ThrowAsync<DbUpdateException>(() => CreateService(dbContext).SetOwnedAsync(
            volumeId, new SetVolumeOwnedRequest { Owned = true }, CancellationToken.None));

        // One try and one retry, then the central handler answers 409.
        exception.IsUniqueViolation.ShouldBeTrue();
        attempts.ShouldBe(2);
    }

    /// <summary>
    /// A context whose saves after the seed are refused while <paramref name="refuse"/> says so.
    /// </summary>
    private static LnShelfDbContext CreateDbContext(Func<bool> refuse)
    {
        var seeded = false;
        return new(
            new DbContextOptionsBuilder<LnShelfDbContext>()
                .UseInMemoryDatabase($"first-touch-{Guid.CreateVersion7()}")
                .AddInterceptors(new RefusingSaveInterceptor(
                    _ =>
                    {
                        if (!seeded)
                        {
                            seeded = true;
                            return false;
                        }

                        return refuse();
                    },
                    RefusingSaveInterceptor.UniqueViolation))
                .Options,
            TestUsers.Current);
    }

    private static async Task<Guid> SeedVolumeAsync(LnShelfDbContext dbContext)
    {
        var volume = new Volume { Id = Guid.CreateVersion7(), Number = 1, IsReleased = true };
        var series = new Series
        {
            Id = Guid.CreateVersion7(),
            TitleEnglish = "Overlord",
            Format = Format.LightNovel,
            UserState = new SeriesUserState { TrackingStatus = TrackingStatus.Reading },
        };
        series.Volumes.Add(volume);
        dbContext.Series.Add(series);
        await dbContext.SaveChangesAsync();

        // The request starts from a fresh load, as a new scope would.
        dbContext.ChangeTracker.Clear();
        return volume.Id;
    }

    private static VolumeCommandService CreateService(LnShelfDbContext dbContext) =>
        new(dbContext, TimeProvider.System, NullLogger<VolumeCommandService>.Instance);
}
