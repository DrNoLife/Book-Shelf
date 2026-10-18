using LnShelf.Api.Tests.Features.Covers.Fixtures;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;

namespace LnShelf.Api.Tests.Features.Shelf.Services;

/// <summary>
/// A volume's cover override: upload, remove and pick from the covers other users share, and
/// the published row that has to follow every one of them.
/// </summary>
public sealed class VolumeCoverCommandServiceTests
{
    private static readonly Guid Owner = TestUsers.Owner;
    private static readonly Guid Other = TestUsers.SomeoneElse;

    [Fact]
    public async Task TheCoverWrites_OnAnUnknownVolume_ReturnNull()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var missing = Guid.CreateVersion7();

        (await service.SetCoverAsync(missing, CommunityCoverShelf.CreatePng(40, 60), CancellationToken.None)).ShouldBeNull();
        (await service.PickCommunityAsync(missing, new string('a', 64), CancellationToken.None)).ShouldBeNull();
        (await service.RemoveCoverAsync(missing, CancellationToken.None)).ShouldBeNull();
    }

    [Fact]
    public async Task SetCoverAsync_ARealImage_IsNormalizedToWebpAndWins()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, volumeCount: 2);
        var volume = series.Volumes.Single(candidate => candidate.Number == 1);

        // The volume already has synced art, so this proves the override outranks it.
        volume.CoverObjectKey = "anilist/94846.jpg";
        await dbContext.SaveChangesAsync();

        var coverStore = Substitute.For<ICoverStore>();
        var detail = await CreateService(dbContext, coverStore).SetCoverAsync(
            volume.Id, CommunityCoverShelf.CreatePng(600, 900), CancellationToken.None);

        var response = Written(detail, volume.Id);
        response.HasCoverOverride.ShouldBeTrue();
        var objectKey = response.CoverObjectKey.ShouldNotBeNull();
        objectKey.ShouldStartWith($"override/volume/{volume.Id}/");
        objectKey.ShouldEndWith(".webp");
        CoverObjectKey.IsWellFormed(objectKey).ShouldBeTrue();

        await coverStore.Received(1).SaveAsync(
            objectKey,
            Arg.Any<ReadOnlyMemory<byte>>(),
            "image/webp",
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Covers are served as immutable for a week, so a replaced override has to arrive
    /// under a new key or the browser keeps showing the old one.
    /// </summary>
    [Fact]
    public async Task SetCoverAsync_ReplacingAnOverride_UsesANewKeyAndDeletesTheOldObject()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, volumeCount: 1);
        var volumeId = series.Volumes.Single().Id;

        var coverStore = Substitute.For<ICoverStore>();
        var service = CreateService(dbContext, coverStore);

        var first = Written(
            await service.SetCoverAsync(volumeId, CommunityCoverShelf.CreatePng(400, 600), CancellationToken.None), volumeId)
            .CoverObjectKey.ShouldNotBeNull();
        var second = Written(
            await service.SetCoverAsync(volumeId, CommunityCoverShelf.CreatePng(400, 600), CancellationToken.None), volumeId)
            .CoverObjectKey.ShouldNotBeNull();

        second.ShouldNotBe(first);
        await coverStore.Received(1).DeleteAsync(first, Arg.Any<CancellationToken>());
        await coverStore.DidNotReceive().DeleteAsync(second, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveCoverAsync_AnOverriddenVolume_DeletesTheObjectItNoLongerNames()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, volumeCount: 1);
        var volumeId = series.Volumes.Single().Id;

        var coverStore = Substitute.For<ICoverStore>();
        var service = CreateService(dbContext, coverStore);
        var objectKey = Written(
            await service.SetCoverAsync(volumeId, CommunityCoverShelf.CreatePng(400, 600), CancellationToken.None), volumeId)
            .CoverObjectKey.ShouldNotBeNull();

        await service.RemoveCoverAsync(volumeId, CancellationToken.None);

        await coverStore.Received(1).DeleteAsync(objectKey, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetCoverAsync_TheOldObjectCannotBeDeleted_StillSavesTheNewCover()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, volumeCount: 1);
        var volumeId = series.Volumes.Single().Id;

        var coverStore = Substitute.For<ICoverStore>();
        coverStore.DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new HttpRequestException("MinIO is down")));
        var service = CreateService(dbContext, coverStore);

        await service.SetCoverAsync(volumeId, CommunityCoverShelf.CreatePng(400, 600), CancellationToken.None);
        var detail = await service.SetCoverAsync(volumeId, CommunityCoverShelf.CreatePng(400, 600), CancellationToken.None);

        // An orphaned object is harmless; reporting a saved cover as a failure is not.
        (await dbContext.VolumeUserStates.SingleAsync()).CoverOverrideObjectKey
            .ShouldBe(Written(detail, volumeId).CoverObjectKey);
    }

    [Fact]
    public async Task SetCoverAsync_AnOversizedImage_IsCappedOnTheLongEdge()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, volumeCount: 1);

        var coverStore = Substitute.For<ICoverStore>();
        byte[]? stored = null;
        await coverStore.SaveAsync(
            Arg.Any<string>(),
            Arg.Do<ReadOnlyMemory<byte>>(bytes => stored = bytes.ToArray()),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());

        await CreateService(dbContext, coverStore).SetCoverAsync(
            series.Volumes.Single().Id, CommunityCoverShelf.CreatePng(2400, 3600), CancellationToken.None);

        stored.ShouldNotBeNull();
        using var image = Image.Load(stored);

        // 1000 is the configured cap, and the aspect ratio is preserved rather than
        // cropped — a cover's proportions are part of the artwork.
        image.Width.ShouldBeLessThanOrEqualTo(1000);
        image.Height.ShouldBeLessThanOrEqualTo(1000);
        image.Height.ShouldBe(1000);
    }

    [Fact]
    public async Task SetCoverAsync_SomethingThatIsNotAnImage_IsRefused()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, volumeCount: 1);

        var act = async () => await CreateService(dbContext).SetCoverAsync(
            series.Volumes.Single().Id, Encoding.UTF8.GetBytes("this is not a png"), CancellationToken.None);

        await act.ShouldThrowAsync<InvalidInputException>();
    }

    [Fact]
    public async Task RemoveCoverAsync_AnOverriddenVolume_FallsBackToSyncedArt()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, volumeCount: 1);
        var volume = series.Volumes.Single();
        volume.CoverObjectKey = "anilist/94846.jpg";
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext, Substitute.For<ICoverStore>());
        await service.SetCoverAsync(volume.Id, CommunityCoverShelf.CreatePng(400, 600), CancellationToken.None);

        var detail = await service.RemoveCoverAsync(volume.Id, CancellationToken.None);

        var response = Written(detail, volume.Id);
        response.HasCoverOverride.ShouldBeFalse();
        response.CoverObjectKey.ShouldBe("anilist/94846.jpg");
    }

    [Fact]
    public async Task SetCoverAsync_NotSharing_PublishesNothing()
    {
        var database = CommunityCoverShelf.NewDatabase();
        var volumeId = await CommunityCoverShelf.SeedVolumeAsync(database, Owner);

        await using var dbContext = CommunityCoverShelf.CreateDbContext(database, Owner);
        await CommunityCoverShelf.CoverCommands(dbContext, Owner, new InMemoryCoverStore()).SetCoverAsync(
            volumeId, CommunityCoverShelf.CreatePng(400, 600), CancellationToken.None);

        (await dbContext.CommunityCovers.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task SetCoverAsync_Sharing_PublishesTheStoredBytesAndKeepsTheRowInStepOnReplaceAndRemove()
    {
        var database = CommunityCoverShelf.NewDatabase();
        var store = new InMemoryCoverStore();
        var volumeId = await CommunityCoverShelf.SeedVolumeAsync(database, Owner);
        await CommunityCoverShelf.EnableSharingAsync(database, Owner);

        await using var dbContext = CommunityCoverShelf.CreateDbContext(database, Owner);
        var service = CommunityCoverShelf.CoverCommands(dbContext, Owner, store);

        var first = Written(
            await service.SetCoverAsync(volumeId, CommunityCoverShelf.CreatePng(400, 600), CancellationToken.None), volumeId)
            .CoverObjectKey.ShouldNotBeNull();

        var row = await dbContext.CommunityCovers.SingleAsync();
        row.SourceVolumeId.ShouldBe(volumeId);
        row.ObjectKey.ShouldBe(first);
        row.ContentHash.ShouldBe(CoverContentHash.Compute((await store.ReadAsync(first, CancellationToken.None))!.Content));

        var second = Written(
            await service.SetCoverAsync(volumeId, CommunityCoverShelf.CreatePng(500, 700), CancellationToken.None), volumeId)
            .CoverObjectKey.ShouldNotBeNull();

        row = await dbContext.CommunityCovers.SingleAsync();
        row.ObjectKey.ShouldBe(second);
        row.ContentHash.ShouldBe(CoverContentHash.Compute((await store.ReadAsync(second, CancellationToken.None))!.Content));

        await service.RemoveCoverAsync(volumeId, CancellationToken.None);

        (await dbContext.CommunityCovers.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task PickCommunityAsync_AMatchingCover_CopiesTheBytesVerbatimUnderTheUsersOwnKey()
    {
        var database = CommunityCoverShelf.NewDatabase();
        var store = new InMemoryCoverStore();
        var volumeId = await CommunityCoverShelf.SeedVolumeAsync(database, Owner);

        // Not a decodable image: a pick must not run the bytes through the normalizer, or the
        // hash would no longer match what the sharer published.
        byte[] shared = [9, 8, 7, 6, 5];
        var published = await CommunityCoverShelf.PublishAsync(database, Other, store, shared, DateTimeOffset.UtcNow);

        await using var dbContext = CommunityCoverShelf.CreateDbContext(database, Owner);
        var detail = await CommunityCoverShelf.CoverCommands(dbContext, Owner, store)
            .PickCommunityAsync(volumeId, published.ContentHash, CancellationToken.None);

        var key = Written(detail, volumeId).CoverObjectKey.ShouldNotBeNull();
        key.ShouldStartWith($"override/volume/{volumeId}/");
        key.ShouldNotBe(published.ObjectKey);
        var copy = (await store.ReadAsync(key, CancellationToken.None)).ShouldNotBeNull();
        copy.Content.ShouldBe(shared);
        CoverContentHash.Compute(copy.Content).ShouldBe(published.ContentHash);
        (await store.ExistsAsync(published.ObjectKey, CancellationToken.None)).ShouldBeTrue();
    }

    [Fact]
    public async Task PickCommunityAsync_ReplacingAnOverrideWhileSharing_DeletesTheOldObjectAndRepublishes()
    {
        var database = CommunityCoverShelf.NewDatabase();
        var store = new InMemoryCoverStore();
        store.Put("override/volume/old/old.webp", [1, 2, 3]);
        var volumeId = await CommunityCoverShelf.SeedVolumeAsync(
            database, Owner, overrideObjectKey: "override/volume/old/old.webp");
        await CommunityCoverShelf.EnableSharingAsync(database, Owner);
        var published = await CommunityCoverShelf.PublishAsync(database, Other, store, [9, 8, 7], DateTimeOffset.UtcNow);

        await using var dbContext = CommunityCoverShelf.CreateDbContext(database, Owner);
        await CommunityCoverShelf.CoverCommands(dbContext, Owner, store)
            .PickCommunityAsync(volumeId, published.ContentHash, CancellationToken.None);

        (await store.ExistsAsync("override/volume/old/old.webp", CancellationToken.None)).ShouldBeFalse();
        var own = await dbContext.CommunityCovers.SingleAsync(cover => cover.UploaderUserId == Owner);
        own.ContentHash.ShouldBe(published.ContentHash);
    }

    [Fact]
    public async Task PickCommunityAsync_AHashThatMatchesNoSharedCoverForThisVolume_ReturnsNull()
    {
        var database = CommunityCoverShelf.NewDatabase();
        var store = new InMemoryCoverStore();
        var volumeId = await CommunityCoverShelf.SeedVolumeAsync(database, Owner);

        // Published, but for volume 2 and for the manga: not a candidate for this volume.
        var wrongNumber = await CommunityCoverShelf.PublishAsync(database, Other, store, [1], DateTimeOffset.UtcNow, number: 2);
        var wrongFormat = await CommunityCoverShelf.PublishAsync(database, Other, store, [2], DateTimeOffset.UtcNow, format: Format.Manga);

        await using var dbContext = CommunityCoverShelf.CreateDbContext(database, Owner);
        var service = CommunityCoverShelf.CoverCommands(dbContext, Owner, store);

        (await service.PickCommunityAsync(volumeId, wrongNumber.ContentHash, CancellationToken.None)).ShouldBeNull();
        (await service.PickCommunityAsync(volumeId, wrongFormat.ContentHash, CancellationToken.None)).ShouldBeNull();
        (await service.PickCommunityAsync(volumeId, new string('f', 64), CancellationToken.None)).ShouldBeNull();
        (await dbContext.VolumeUserStates.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task PickCommunityAsync_TheCoverLimitIsReachedAndTheVolumeHasNoOverride_IsRefused()
    {
        var database = CommunityCoverShelf.NewDatabase();
        var store = new InMemoryCoverStore();
        await CommunityCoverShelf.SeedVolumeAsync(database, Owner, number: 5, overrideObjectKey: "override/volume/x/x.webp");
        var volumeId = await CommunityCoverShelf.SeedVolumeAsync(database, Owner);
        var published = await CommunityCoverShelf.PublishAsync(database, Other, store, [9, 8, 7], DateTimeOffset.UtcNow);

        await using var dbContext = CommunityCoverShelf.CreateDbContext(database, Owner);
        var limits = new UserLimits(dbContext, Options.Create(new UserLimitOptions { MaximumCoverOverridesPerUser = 1 }));

        var act = async () => await CommunityCoverShelf.CoverCommands(dbContext, Owner, store, limits)
            .PickCommunityAsync(volumeId, published.ContentHash, CancellationToken.None);

        await act.ShouldThrowAsync<ConflictException>();
    }

    /// <summary>
    /// The volume a write was about, read back out of the series it answers with.
    /// </summary>
    private static VolumeResponse Written(SeriesDetailResponse? detail, Guid volumeId) =>
        detail.ShouldNotBeNull().Volumes.Single(volume => volume.VolumeId == volumeId);

    private static VolumeCoverCommandService CreateService(
        LnShelfDbContext dbContext,
        ICoverStore? coverStore = null) =>
        CommunityCoverShelf.CoverCommands(dbContext, Owner, coverStore ?? Substitute.For<ICoverStore>());

    private static async Task<Series> SeedAsync(LnShelfDbContext dbContext, int volumeCount)
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

        foreach (var number in Enumerable.Range(1, volumeCount))
        {
            series.Volumes.Add(new Volume { Id = Guid.CreateVersion7(), Number = number, IsReleased = true });
        }

        dbContext.Series.Add(series);
        await dbContext.SaveChangesAsync();

        return series;
    }

    private static LnShelfDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<LnShelfDbContext>()
            .UseInMemoryDatabase($"volume-covers-{Guid.CreateVersion7()}")
            .Options, TestUsers.Current);
}
