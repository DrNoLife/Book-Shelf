using LnShelf.Api.Tests.Features.Covers.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace LnShelf.Api.Tests.Features.Covers.Services;

/// <summary>
/// Turning sharing on and off, hiding, and the owner's console withdrawal. Every write is
/// scoped by hand to the acting user, so each test checks the other user's rows too.
/// </summary>
public sealed class CommunityCoverCommandServiceTests
{
    private static readonly Guid Owner = TestUsers.Owner;
    private static readonly Guid Other = TestUsers.SomeoneElse;

    private static readonly byte[] FirstImage = [1, 1, 1];
    private static readonly byte[] SecondImage = [2, 2, 2];

    [Fact]
    public async Task SetSharingAsync_On_PublishesTheCoversAlreadyUploadedForCataloguedVolumes()
    {
        var database = CommunityCoverShelf.NewDatabase();
        var store = new InMemoryCoverStore();
        store.Put("override/volume/a/one.webp", FirstImage);
        store.Put("override/volume/b/two.webp", SecondImage);
        var catalogued = await CommunityCoverShelf.SeedVolumeAsync(
            database, Owner, overrideObjectKey: "override/volume/a/one.webp");

        // A manual series has no id another user could match, so there is nothing to publish.
        await CommunityCoverShelf.SeedVolumeAsync(
            database, Owner, aniListId: null, overrideObjectKey: "override/volume/b/two.webp");

        await using var dbContext = CommunityCoverShelf.CreateDbContext(database, Owner);
        var response = await CommunityCoverShelf.Commands(dbContext, Owner, store)
            .SetSharingAsync(true, CancellationToken.None);

        response.Enabled.ShouldBeTrue();
        response.Since.ShouldNotBeNull();
        response.SharedCount.ShouldBe(1);

        var row = await dbContext.CommunityCovers.SingleAsync();
        row.SourceVolumeId.ShouldBe(catalogued);
        row.UploaderUserId.ShouldBe(Owner);
        row.AniListId.ShouldBe(94846);
        row.Format.ShouldBe(Format.LightNovel);
        row.Number.ShouldBe(1);
        row.ContentHash.ShouldBe(CoverContentHash.Compute(FirstImage));
        row.ObjectKey.ShouldBe("override/volume/a/one.webp");
    }

    [Fact]
    public async Task RefreshSeriesIdsAsync_ARelinkedSeries_UpdatesOnlyTheCallersRowsForThatSeries()
    {
        var database = CommunityCoverShelf.NewDatabase();
        var store = new InMemoryCoverStore();
        var relinkedVolume = await CommunityCoverShelf.SeedVolumeAsync(database, Owner, mangaUpdatesId: 1);
        var otherSeriesVolume = await CommunityCoverShelf.SeedVolumeAsync(database, Owner, number: 2, mangaUpdatesId: 1);
        var own = await PublishForAsync(database, Owner, relinkedVolume);
        var ownOnAnotherSeries = await PublishForAsync(database, Owner, otherSeriesVolume);
        var someoneElses = await CommunityCoverShelf.PublishAsync(
            database, Other, store, FirstImage, DateTimeOffset.UtcNow, mangaUpdatesId: 1);

        await using var dbContext = CommunityCoverShelf.CreateDbContext(database, Owner);
        var series = await dbContext.Series.SingleAsync(candidate => candidate.Volumes.Any(volume => volume.Id == relinkedVolume));
        series.MangaUpdatesId = 2;
        await CommunityCoverShelf.Commands(dbContext, Owner, store).RefreshSeriesIdsAsync(series, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        await using var saved = CommunityCoverShelf.CreateDbContext(database, Owner);
        var rows = await saved.CommunityCovers.AsNoTracking().ToDictionaryAsync(cover => cover.Id);
        rows[own].MangaUpdatesId.ShouldBe(2);
        rows[ownOnAnotherSeries].MangaUpdatesId.ShouldBe(1);
        rows[someoneElses.Id].MangaUpdatesId.ShouldBe(1);
    }

    [Fact]
    public async Task SetSharingAsync_OnAgain_KeepsTheOriginalConsentTimestamp()
    {
        var database = CommunityCoverShelf.NewDatabase();
        var store = new InMemoryCoverStore();
        await CommunityCoverShelf.EnableSharingAsync(database, Owner);

        await using var dbContext = CommunityCoverShelf.CreateDbContext(database, Owner);
        var response = await CommunityCoverShelf.Commands(dbContext, Owner, store)
            .SetSharingAsync(true, CancellationToken.None);

        response.Since.ShouldBe(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task SetSharingAsync_Off_RemovesOnlyTheCallersRowsAndClearsTheConsent()
    {
        var database = CommunityCoverShelf.NewDatabase();
        var store = new InMemoryCoverStore();
        await CommunityCoverShelf.EnableSharingAsync(database, Owner);
        await CommunityCoverShelf.PublishAsync(database, Owner, store, FirstImage, DateTimeOffset.UtcNow);
        await CommunityCoverShelf.PublishAsync(database, Other, store, FirstImage, DateTimeOffset.UtcNow);

        await using var dbContext = CommunityCoverShelf.CreateDbContext(database, Owner);
        var response = await CommunityCoverShelf.Commands(dbContext, Owner, store)
            .SetSharingAsync(false, CancellationToken.None);

        response.Enabled.ShouldBeFalse();
        response.Since.ShouldBeNull();
        response.SharedCount.ShouldBe(0);

        (await dbContext.UserPreferences.SingleAsync()).CoverSharingSince.ShouldBeNull();
        (await dbContext.CommunityCovers.SingleAsync()).UploaderUserId.ShouldBe(Other);
    }

    [Fact]
    public async Task GetSharingAsync_CountsOnlyTheCallersOwnPublishedCovers()
    {
        var database = CommunityCoverShelf.NewDatabase();
        var store = new InMemoryCoverStore();
        await CommunityCoverShelf.EnableSharingAsync(database, Owner);
        await CommunityCoverShelf.PublishAsync(database, Owner, store, FirstImage, DateTimeOffset.UtcNow);
        await CommunityCoverShelf.PublishAsync(database, Other, store, SecondImage, DateTimeOffset.UtcNow);

        await using var dbContext = CommunityCoverShelf.CreateDbContext(database, Owner);
        var response = await CommunityCoverShelf.Queries(dbContext, Owner, store).GetSharingAsync(CancellationToken.None);

        response.Enabled.ShouldBeTrue();
        response.SharedCount.ShouldBe(1);
    }

    [Fact]
    public async Task HideAsync_TwiceInARow_LeavesOneHide()
    {
        var database = CommunityCoverShelf.NewDatabase();
        var hash = CoverContentHash.Compute(FirstImage);

        await using var dbContext = CommunityCoverShelf.CreateDbContext(database, Owner);
        var commands = CommunityCoverShelf.Commands(dbContext, Owner, new InMemoryCoverStore());
        await commands.HideAsync(hash, CancellationToken.None);
        await commands.HideAsync(hash, CancellationToken.None);

        (await dbContext.CommunityCoverHides.SingleAsync()).ContentHash.ShouldBe(hash);
    }

    [Fact]
    public async Task HideAsync_ForOneUser_DoesNotHideItForAnother()
    {
        var database = CommunityCoverShelf.NewDatabase();
        var hash = CoverContentHash.Compute(FirstImage);

        await using (var ownerContext = CommunityCoverShelf.CreateDbContext(database, Owner))
        {
            await CommunityCoverShelf.Commands(ownerContext, Owner, new InMemoryCoverStore())
                .HideAsync(hash, CancellationToken.None);
        }

        await using var otherContext = CommunityCoverShelf.CreateDbContext(database, Other);
        await CommunityCoverShelf.Commands(otherContext, Other, new InMemoryCoverStore())
            .HideAsync(hash, CancellationToken.None);

        (await otherContext.CommunityCoverHides.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task UnhideAsync_AHiddenCoverAndOneThatIsNot_BothSucceed()
    {
        var database = CommunityCoverShelf.NewDatabase();
        var hash = CoverContentHash.Compute(FirstImage);

        await using var dbContext = CommunityCoverShelf.CreateDbContext(database, Owner);
        var commands = CommunityCoverShelf.Commands(dbContext, Owner, new InMemoryCoverStore());
        await commands.HideAsync(hash, CancellationToken.None);

        await commands.UnhideAsync(hash, CancellationToken.None);
        await commands.UnhideAsync(hash, CancellationToken.None);

        (await dbContext.CommunityCoverHides.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task WithdrawAsync_AHash_DeletesEveryUsersRowWithThatHashOnly()
    {
        var database = CommunityCoverShelf.NewDatabase();
        var store = new InMemoryCoverStore();
        await CommunityCoverShelf.PublishAsync(database, Owner, store, FirstImage, DateTimeOffset.UtcNow);
        await CommunityCoverShelf.PublishAsync(database, Other, store, FirstImage, DateTimeOffset.UtcNow);
        await CommunityCoverShelf.PublishAsync(database, Other, store, SecondImage, DateTimeOffset.UtcNow);

        await using var dbContext = CommunityCoverShelf.CreateDbContext(database, Owner);
        var withdrawn = await CommunityCoverShelf.Commands(dbContext, Owner, store)
            .WithdrawAsync(CoverContentHash.Compute(FirstImage), CancellationToken.None);

        withdrawn.ShouldBe(2);
        (await dbContext.CommunityCovers.SingleAsync()).ContentHash.ShouldBe(CoverContentHash.Compute(SecondImage));
    }

    /// <summary>A published row for one of the user's own volumes, carrying the old id 1.</summary>
    private static async Task<Guid> PublishForAsync(string database, Guid uploaderId, Guid sourceVolumeId)
    {
        await using var dbContext = CommunityCoverShelf.CreateDbContext(database, uploaderId);
        var cover = new CommunityCover
        {
            Id = Guid.CreateVersion7(),
            UploaderUserId = uploaderId,
            SourceVolumeId = sourceVolumeId,
            AniListId = 94846,
            MangaUpdatesId = 1,
            Format = Format.LightNovel,
            Number = 1,
            ContentHash = CoverContentHash.Compute(FirstImage),
            ObjectKey = CoverObjectKey.ForVolumeOverride(sourceVolumeId, Guid.CreateVersion7(), ".webp"),
            PublishedAt = DateTimeOffset.UtcNow,
        };
        dbContext.CommunityCovers.Add(cover);
        await dbContext.SaveChangesAsync();

        return cover.Id;
    }
}
