using LnShelf.Api.Tests.Data.Postgres;
using Microsoft.EntityFrameworkCore;

namespace LnShelf.Api.Tests.Features.Shelf.Services;

/// <summary>
/// Two requests touching an untouched volume at once both land (audit #23).
/// </summary>
// Two scopes on a real Postgres: the loser loads before the winner saves, then meets the
// winner's state row at its own save. Without the retry that is a 409 and a lost click.
[Collection(PostgresCollection.Name)]
public sealed class VolumeCommandServiceRaceTests(PostgresDatabase database)
{
    [PostgresFact]
    public async Task SetReadAsync_AConcurrentOwnedWriteCreatesTheStateRowFirst_AppliesTheReadOnTopOfIt()
    {
        var owner = await database.CreateUserAsync();
        var volumeId = (await database.CreateSeriesAsync(owner, volumeCount: 2)).Volumes.Single(volume => volume.Number == 1).Id;

        await using var winnerContext = database.CreateContext(owner);
        await using var loserContext = database.CreateContext(
            owner,
            new BeforeFirstSaveInterceptor(token => CreateService(winnerContext).SetOwnedAsync(
                volumeId, new SetVolumeOwnedRequest { Owned = true }, token)));

        var detail = await CreateService(loserContext).SetReadAsync(
            volumeId, new SetVolumeReadRequest { Status = ReadStatus.Read }, CancellationToken.None);

        var answered = detail.ShouldNotBeNull().Volumes.Single(volume => volume.VolumeId == volumeId);
        answered.ReadStatus.ShouldBe(ReadStatus.Read);
        answered.Owned.ShouldBeTrue();

        var stored = await StoredStateAsync(owner, volumeId);
        stored.ReadStatus.ShouldBe(ReadStatus.Read);
        stored.Owned.ShouldBeTrue();
    }

    [PostgresFact]
    public async Task SetOwnedAsync_AConcurrentReadWriteCreatesTheStateRowFirst_AppliesTheOwnershipOnTopOfIt()
    {
        var owner = await database.CreateUserAsync();
        var volumeId = (await database.CreateSeriesAsync(owner, volumeCount: 1)).Volumes.Single().Id;

        await using var winnerContext = database.CreateContext(owner);
        await using var loserContext = database.CreateContext(
            owner,
            new BeforeFirstSaveInterceptor(token => CreateService(winnerContext).SetReadAsync(
                volumeId, new SetVolumeReadRequest { Status = ReadStatus.Read }, token)));

        await CreateService(loserContext).SetOwnedAsync(
            volumeId, new SetVolumeOwnedRequest { Owned = true, Format = OwnedFormat.Digital }, CancellationToken.None);

        var stored = await StoredStateAsync(owner, volumeId);
        stored.ReadStatus.ShouldBe(ReadStatus.Read);
        stored.Owned.ShouldBeTrue();
        stored.OwnedFormat.ShouldBe(OwnedFormat.Digital);
    }

    [PostgresFact]
    public async Task SetBulkAsync_AConcurrentWriteCreatesOneStateRowFirst_WritesEveryVolumeOnTopOfIt()
    {
        var owner = await database.CreateUserAsync();
        var series = await database.CreateSeriesAsync(owner, volumeCount: 3);
        var firstVolumeId = series.Volumes.Single(volume => volume.Number == 1).Id;

        await using var winnerContext = database.CreateContext(owner);
        await using var loserContext = database.CreateContext(
            owner,
            new BeforeFirstSaveInterceptor(token => CreateService(winnerContext).SetReadAsync(
                firstVolumeId, new SetVolumeReadRequest { Status = ReadStatus.Read }, token)));

        await CreateService(loserContext).SetBulkAsync(
            series.Id, new BulkVolumeStateRequest { ThroughNumber = 3, Owned = true }, CancellationToken.None);

        await using var dbContext = database.CreateContext(owner);
        var states = await dbContext.Volumes
            .Where(volume => volume.SeriesId == series.Id)
            .Select(volume => new { volume.Number, volume.UserState!.Owned, volume.UserState.ReadStatus })
            .ToListAsync();

        states.ShouldAllBe(state => state.Owned);
        states.Single(state => state.Number == 1).ReadStatus.ShouldBe(ReadStatus.Read);
    }

    private async Task<VolumeUserState> StoredStateAsync(Guid owner, Guid volumeId)
    {
        await using var dbContext = database.CreateContext(owner);
        return await dbContext.VolumeUserStates.AsNoTracking().SingleAsync(state => state.VolumeId == volumeId);
    }

    private static VolumeCommandService CreateService(LnShelfDbContext dbContext) =>
        new(dbContext, TimeProvider.System, NullLogger<VolumeCommandService>.Instance);
}
