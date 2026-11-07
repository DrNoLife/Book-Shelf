using Microsoft.EntityFrameworkCore;

namespace LnShelf.Api.Tests.Data.Postgres;

/// <summary>
/// Deleting a root takes its children with it in the database, not in EF's change tracker.
/// </summary>
// ExecuteDelete never loads the children, so only the foreign keys' ON DELETE CASCADE can
// remove them. OwnerFiltersTests asserts the model says Cascade; these prove the migrated
// schema does it.
[Collection(PostgresCollection.Name)]
public sealed class CascadeDeleteTests(PostgresDatabase database)
{
    [PostgresFact]
    public async Task ExecuteDeleteAsync_ASeries_RemovesItsVolumesAndEveryUserState()
    {
        var owner = await database.CreateUserAsync();
        var series = await database.CreateSeriesAsync(owner, volumeCount: 2);
        var volumeIds = series.Volumes.Select(volume => volume.Id).ToList();

        await using (var seedContext = database.CreateContext(owner))
        {
            seedContext.VolumeUserStates.AddRange(
                volumeIds.Select(volumeId => new VolumeUserState { VolumeId = volumeId, ReadStatus = ReadStatus.Read }));
            await seedContext.SaveChangesAsync();
        }

        await using var dbContext = database.CreateContext(owner);
        await dbContext.Series.Where(candidate => candidate.Id == series.Id).ExecuteDeleteAsync();

        (await dbContext.Volumes.CountAsync(volume => volume.SeriesId == series.Id)).ShouldBe(0);
        (await dbContext.SeriesUserStates.CountAsync(state => state.SeriesId == series.Id)).ShouldBe(0);

        // Unfiltered and reached only through its volume, so counted by key.
        (await dbContext.VolumeUserStates.CountAsync(state => volumeIds.Contains(state.VolumeId))).ShouldBe(0);
    }

    [PostgresFact]
    public async Task ExecuteDeleteAsync_ASyncRun_RemovesItsDiscrepanciesAndTheirValues()
    {
        var owner = await database.CreateUserAsync();
        var runId = Guid.CreateVersion7();
        var discrepancyId = Guid.CreateVersion7();

        await using (var seedContext = database.CreateContext(owner))
        {
            seedContext.SyncRuns.Add(new SyncRun
            {
                Id = runId,
                StartedAt = DateTimeOffset.UnixEpoch,
                Summary = "1 series refreshed",
                Discrepancies =
                [
                    new SyncDiscrepancy
                    {
                        Id = discrepancyId,
                        SeriesId = Guid.CreateVersion7(),
                        SeriesTitle = "Overlord",
                        Field = SyncDiscrepancyField.VolumeCount,
                        Values =
                        [
                            new SyncDiscrepancyValue { Id = Guid.CreateVersion7(), Source = MetadataSourceName.AniList, Value = "17" },
                            new SyncDiscrepancyValue { Id = Guid.CreateVersion7(), Source = MetadataSourceName.MangaUpdates, Value = "16" },
                        ],
                    },
                ],
            });
            await seedContext.SaveChangesAsync();
        }

        await using var dbContext = database.CreateContext(owner);
        await dbContext.SyncRuns.Where(run => run.Id == runId).ExecuteDeleteAsync();

        (await dbContext.SyncDiscrepancies.CountAsync(discrepancy => discrepancy.SyncRunId == runId)).ShouldBe(0);
        (await dbContext.SyncDiscrepancyValues.CountAsync(value => value.SyncDiscrepancyId == discrepancyId)).ShouldBe(0);
    }
}
