using LnShelf.Api.Features.Notifications.Models;
using LnShelf.Api.Features.Preferences.Models;
using LnShelf.Api.Features.Recommendations.Models;
using Microsoft.EntityFrameworkCore;

namespace LnShelf.Api.Tests.Data.Postgres;

/// <summary>
/// Every owned root, and the children filtered through one, is invisible to another user
/// when the filters are translated to SQL.
/// </summary>
// OwnerFiltersTests proves every entity has a filter or a reason; InMemory evaluates those
// filters in .NET. These run them as Npgsql's SQL, where a filter through a navigation
// (a volume through its series) becomes a join that could be translated wrongly.
[Collection(PostgresCollection.Name)]
public sealed class OwnerFilterQueryTests(PostgresDatabase database)
{
    [PostgresFact]
    public async Task Queries_TwoUsersWithAFullShelfEach_ReadOnlyTheCurrentUsersOwnRows()
    {
        var owner = await database.CreateUserAsync();
        var someoneElse = await database.CreateUserAsync();
        await SeedEveryRootAsync(owner);
        await SeedEveryRootAsync(someoneElse);

        await using var dbContext = database.CreateContext(owner);

        (await dbContext.Series.Select(series => series.OwnerId).ToListAsync()).ShouldBe([owner]);
        (await dbContext.ImportBatches.Select(batch => batch.OwnerId).ToListAsync()).ShouldBe([owner]);
        (await dbContext.SyncRuns.Select(run => run.OwnerId).ToListAsync()).ShouldBe([owner]);
        (await dbContext.UserPreferences.Select(preferences => preferences.OwnerId).ToListAsync()).ShouldBe([owner]);
        (await dbContext.PushSubscriptions.Select(subscription => subscription.OwnerId).ToListAsync()).ShouldBe([owner]);
        (await dbContext.RecommendationDismissals.Select(dismissal => dismissal.OwnerId).ToListAsync()).ShouldBe([owner]);
        (await dbContext.CommunityCoverHides.Select(hide => hide.OwnerId).ToListAsync()).ShouldBe([owner]);

        // Filtered through their series.
        (await dbContext.Volumes.Select(volume => volume.Series!.OwnerId).Distinct().ToListAsync()).ShouldBe([owner]);
        (await dbContext.SeriesUserStates.Select(state => state.Series!.OwnerId).ToListAsync()).ShouldBe([owner]);
    }

    [PostgresFact]
    public async Task Queries_AScopeWithNoUser_ReadsNothing()
    {
        var owner = await database.CreateUserAsync();
        await SeedEveryRootAsync(owner);

        await using var dbContext = database.CreateContext(userId: null);

        // A forgotten ActAs reads an empty shelf rather than everyone's (LnShelfDbContext).
        (await dbContext.Series.AnyAsync()).ShouldBeFalse();
        (await dbContext.Volumes.AnyAsync()).ShouldBeFalse();
        (await dbContext.SyncRuns.AnyAsync()).ShouldBeFalse();
    }

    /// <summary>One row of each of the seven owned roots on <paramref name="userId"/>'s shelf.</summary>
    private async Task SeedEveryRootAsync(Guid userId)
    {
        await database.CreateSeriesAsync(userId, volumeCount: 2);

        await using var dbContext = database.CreateContext(userId);
        dbContext.ImportBatches.Add(new ImportBatch
        {
            Id = Guid.CreateVersion7(),
            FileName = "export.csv",
            UploadedAt = DateTimeOffset.UnixEpoch,
        });
        dbContext.SyncRuns.Add(new SyncRun { Id = Guid.CreateVersion7(), StartedAt = DateTimeOffset.UnixEpoch, Summary = "nightly" });
        dbContext.UserPreferences.Add(new UserPreferences());
        dbContext.PushSubscriptions.Add(new PushSubscription
        {
            Id = Guid.CreateVersion7(),
            Endpoint = "https://fcm.googleapis.com/fcm/send/shared-browser",
            P256dh = "key",
            Auth = "secret",
        });
        dbContext.RecommendationDismissals.Add(new RecommendationDismissal
        {
            Id = Guid.CreateVersion7(),
            AniListId = 21,
            Title = "One Piece",
            Format = Format.Manga,
        });
        dbContext.CommunityCoverHides.Add(new CommunityCoverHide
        {
            Id = Guid.CreateVersion7(),
            ContentHash = new string('a', CoverContentHash.Length),
        });
        await dbContext.SaveChangesAsync();
    }
}
