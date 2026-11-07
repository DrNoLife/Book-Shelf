using Microsoft.EntityFrameworkCore;

namespace LnShelf.Api.Tests.Data.Postgres;

/// <summary>
/// The unique indexes and keys the race recoveries rely on, enforced by the real schema
/// and recognised by <see cref="DbUpdateExceptionExtensions"/>.
/// </summary>
// InMemory enforces none of these, so every unit test of a race fakes the refusal. These
// prove the refusal really happens, and really reads as a unique violation, on Postgres.
[Collection(PostgresCollection.Name)]
public sealed class UniqueIndexTests(PostgresDatabase database)
{
    [PostgresFact]
    public async Task SaveChangesAsync_TheSameAniListSeriesTwiceOnOneShelf_IsAUniqueViolation()
    {
        var owner = await database.CreateUserAsync();
        await database.CreateSeriesAsync(owner, volumeCount: 1, aniListId: 1001);

        var exception = await Should.ThrowAsync<DbUpdateException>(
            () => database.CreateSeriesAsync(owner, volumeCount: 1, aniListId: 1001));

        exception.IsUniqueViolation.ShouldBeTrue();
    }

    [PostgresFact]
    public async Task SaveChangesAsync_TheSameAniListSeriesOnTwoShelves_IsAllowed()
    {
        var owner = await database.CreateUserAsync();
        var someoneElse = await database.CreateUserAsync();

        await database.CreateSeriesAsync(owner, volumeCount: 1, aniListId: 1002);
        await database.CreateSeriesAsync(someoneElse, volumeCount: 1, aniListId: 1002);

        await using var dbContext = database.CreateContext(someoneElse);
        (await dbContext.Series.CountAsync()).ShouldBe(1);
    }

    [PostgresFact]
    public async Task SaveChangesAsync_TwoVolumesWithOneNumberInASeries_IsAUniqueViolation()
    {
        var owner = await database.CreateUserAsync();
        var series = await database.CreateSeriesAsync(owner, volumeCount: 1);

        await using var dbContext = database.CreateContext(owner);
        dbContext.Volumes.Add(new Volume { Id = Guid.CreateVersion7(), SeriesId = series.Id, Number = 1 });

        var exception = await Should.ThrowAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());

        exception.IsUniqueViolation.ShouldBeTrue();
    }

    /// <summary>
    /// The first-touch race (audit #23): two scopes each create the state row an untouched
    /// volume does not have yet, and the primary key refuses the second.
    /// </summary>
    // A primary key refusal is SQLSTATE 23505 too, which is what lets the retry in
    // FirstTouchRetry catch it with the same check as a unique index.
    [PostgresFact]
    public async Task SaveChangesAsync_TwoScopesCreatingOneVolumesState_IsAUniqueViolation()
    {
        var owner = await database.CreateUserAsync();
        var volumeId = (await database.CreateSeriesAsync(owner, volumeCount: 1)).Volumes.Single().Id;

        await using var first = database.CreateContext(owner);
        await using var second = database.CreateContext(owner);
        first.VolumeUserStates.Add(new VolumeUserState { VolumeId = volumeId, ReadStatus = ReadStatus.Read });
        second.VolumeUserStates.Add(new VolumeUserState { VolumeId = volumeId, Owned = true });

        await first.SaveChangesAsync();
        var exception = await Should.ThrowAsync<DbUpdateException>(() => second.SaveChangesAsync());

        exception.IsUniqueViolation.ShouldBeTrue();
    }
}
