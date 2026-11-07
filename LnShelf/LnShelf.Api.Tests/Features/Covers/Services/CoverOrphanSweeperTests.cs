using LnShelf.Api.Features.Covers.Commands;
using LnShelf.Api.Features.Covers.Services;
using LnShelf.Api.Tests.Features.Covers.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LnShelf.Api.Tests.Features.Covers.Services;

/// <summary>
/// The one-off that removes uploaded covers whose volume is gone from every shelf. It deletes
/// user data, so what it must leave alone matters as much as what it removes.
/// </summary>
public sealed class CoverOrphanSweeperTests
{
    [Fact]
    public async Task RunAsync_UploadsOfDeletedVolumes_AreDeletedAndLiveOnesOfAnyUserAreKept()
    {
        var database = $"sweep-{Guid.CreateVersion7()}";
        var ownersVolume = Guid.CreateVersion7();
        var someoneElsesVolume = Guid.CreateVersion7();
        var deletedVolume = Guid.CreateVersion7();

        var ownersCover = Upload(ownersVolume);
        var someoneElsesCover = Upload(someoneElsesVolume);
        var orphan = Upload(deletedVolume);
        await AddVolumeAsync(database, TestUsers.Owner, ownersVolume, ownersCover);
        await AddVolumeAsync(database, TestUsers.SomeoneElse, someoneElsesVolume, someoneElsesCover);
        var coverStore = StoreHolding(ownersCover, someoneElsesCover, orphan);

        // No user is chosen for the run: "whose shelf has it" is not a question one shelf
        // answers, so the sweeper visits every user's shelf itself.
        var report = await Sweeper(database, coverStore)
            .RunAsync(dryRun: false, CancellationToken.None);

        report.ShouldBe(new CoverOrphanSweepReport(Scanned: 3, Orphaned: 1, Deleted: 1, Failed: 0));
        await coverStore.Received(1).DeleteAsync(orphan, Arg.Any<CancellationToken>());
        await coverStore.DidNotReceive().DeleteAsync(ownersCover, Arg.Any<CancellationToken>());
        await coverStore.DidNotReceive().DeleteAsync(someoneElsesCover, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_AKeyThatIsNotAnUpload_IsLeftAlone()
    {
        var coverStore = StoreHolding("override/volume/not-a-guid/file.webp", "override/stray.webp");

        var report = await Sweeper($"sweep-{Guid.CreateVersion7()}", coverStore)
            .RunAsync(dryRun: false, CancellationToken.None);

        report.Orphaned.ShouldBe(0);
        await coverStore.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_DryRun_CountsOrphansAndDeletesNothing()
    {
        var coverStore = StoreHolding(Upload(Guid.CreateVersion7()), Upload(Guid.CreateVersion7()));

        var report = await Sweeper($"sweep-{Guid.CreateVersion7()}", coverStore)
            .RunAsync(dryRun: true, CancellationToken.None);

        report.ShouldBe(new CoverOrphanSweepReport(Scanned: 2, Orphaned: 2, Deleted: 0, Failed: 0));
        await coverStore.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // One object the store will not delete must not stop the rest, and must show in the
    // report so the exit code says the run was not clean.
    [Fact]
    public async Task RunAsync_OneDeleteFails_RestAreStillDeletedAndTheFailureIsCounted()
    {
        var stuck = Upload(Guid.CreateVersion7());
        var other = Upload(Guid.CreateVersion7());
        var coverStore = StoreHolding(stuck, other);
        coverStore.DeleteAsync(stuck, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("The cover store is down")));

        var report = await Sweeper($"sweep-{Guid.CreateVersion7()}", coverStore)
            .RunAsync(dryRun: false, CancellationToken.None);

        report.ShouldBe(new CoverOrphanSweepReport(Scanned: 2, Orphaned: 2, Deleted: 1, Failed: 1));
        await coverStore.Received(1).DeleteAsync(other, Arg.Any<CancellationToken>());
    }

    // A volume added and given a cover while the sweep runs: the store lists the upload, and
    // the volume exists by the time the sweep asks, so its fresh cover must survive.
    [Fact]
    public async Task RunAsync_AVolumeAddedWhileTheStoreIsListed_KeepsItsUpload()
    {
        var database = $"sweep-{Guid.CreateVersion7()}";
        var addedVolume = Guid.CreateVersion7();
        var freshUpload = Upload(addedVolume);
        var coverStore = Substitute.For<ICoverStore>();
        coverStore.ListKeysAsync("override/volume/", Arg.Any<CancellationToken>())
            .Returns(ListingThenAdding(freshUpload, () => AddVolumeAsync(database, TestUsers.Owner, addedVolume)));

        var report = await Sweeper(database, coverStore)
            .RunAsync(dryRun: false, CancellationToken.None);

        report.Orphaned.ShouldBe(0);
        await coverStore.DidNotReceive().DeleteAsync(freshUpload, Arg.Any<CancellationToken>());
    }

    // An avatar is live while any account names it, as its provider copy or as its upload;
    // a replaced one whose delete failed, or one left by a deleted account, is not.
    [Fact]
    public async Task RunAsync_Avatars_ReferencedOnesAreKeptAndTheRestDeleted()
    {
        var database = $"sweep-{Guid.CreateVersion7()}";
        var providerCopy = CoverObjectKey.ForAvatar(TestUsers.Owner, Guid.CreateVersion7());
        var upload = CoverObjectKey.ForAvatar(TestUsers.Owner, Guid.CreateVersion7());
        var replaced = CoverObjectKey.ForAvatar(TestUsers.Owner, Guid.CreateVersion7());
        var ofADeletedAccount = CoverObjectKey.ForAvatar(Guid.CreateVersion7(), Guid.CreateVersion7());
        await AddUserAsync(database, TestUsers.Owner, providerCopy, upload);

        var coverStore = StoreHolding();
        coverStore.ListKeysAsync(CoverObjectKey.AvatarPrefix, Arg.Any<CancellationToken>())
            .Returns(ToAsync([providerCopy, upload, replaced, ofADeletedAccount, "avatar/stray.webp"]));

        var report = await Sweeper(database, coverStore).RunAsync(dryRun: false, CancellationToken.None);

        report.ShouldBe(new CoverOrphanSweepReport(Scanned: 5, Orphaned: 2, Deleted: 2, Failed: 0));
        await coverStore.Received(1).DeleteAsync(replaced, Arg.Any<CancellationToken>());
        await coverStore.Received(1).DeleteAsync(ofADeletedAccount, Arg.Any<CancellationToken>());
        await coverStore.DidNotReceive().DeleteAsync(providerCopy, Arg.Any<CancellationToken>());
        await coverStore.DidNotReceive().DeleteAsync(upload, Arg.Any<CancellationToken>());
        await coverStore.DidNotReceive().DeleteAsync("avatar/stray.webp", Arg.Any<CancellationToken>());
    }

    // A replaced cover whose delete failed, or an upload whose save failed, sits under a volume
    // that is still alive; only the keys the shelves name can tell it is an orphan. One written
    // within the grace period may still have its save on the way and is kept.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunAsync_UnreferencedUploadsOnALiveVolume_AreOrphansOncePastTheGracePeriod(bool dryRun)
    {
        var database = $"sweep-{Guid.CreateVersion7()}";
        var volumeId = Guid.CreateVersion7();
        var current = Upload(volumeId);
        var replaced = Upload(volumeId);
        var fresh = Upload(volumeId);
        var shared = Upload(volumeId);
        await AddVolumeAsync(database, TestUsers.Owner, volumeId, current);
        await AddPublishedCoverAsync(database, volumeId, shared);

        var coverStore = new InMemoryCoverStore();
        var longAgo = DateTimeOffset.UtcNow - CoverOrphanSweeper.UnreferencedUploadGracePeriod - TimeSpan.FromMinutes(5);
        foreach (var key in new[] { current, replaced, fresh, shared })
        {
            coverStore.Put(key, [1, 2, 3]);
        }

        coverStore.SetLastModified(current, longAgo);
        coverStore.SetLastModified(replaced, longAgo);
        coverStore.SetLastModified(shared, longAgo);

        var report = await Sweeper(database, coverStore).RunAsync(dryRun, CancellationToken.None);

        report.ShouldBe(new CoverOrphanSweepReport(Scanned: 4, Orphaned: 1, Deleted: dryRun ? 0 : 1, Failed: 0));
        coverStore.Keys.ShouldBe(dryRun ? [current, replaced, fresh, shared] : [current, fresh, shared], ignoreOrder: true);
    }

    [Fact]
    public void Command_WorksAcrossEveryShelf_SoTheRunnerPicksNoUser()
    {
        var command = new CoverOrphanSweepCommand(null!);

        command.Name.ShouldBe("cover-orphan-sweep");
        command.ActsOnOneShelf.ShouldBeFalse();
    }

    private static CoverOrphanSweeper Sweeper(string database, ICoverStore coverStore)
    {
        var services = new ServiceCollection();
        services.AddScoped<ICurrentUser>(_ => TestUsers.As(null));
        services.AddDbContext<LnShelfDbContext>(options => options.UseInMemoryDatabase(database));

        return new CoverOrphanSweeper(
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            coverStore,
            TimeProvider.System,
            NullLogger<CoverOrphanSweeper>.Instance);
    }

    private static string Upload(Guid volumeId) =>
        CoverObjectKey.ForVolumeOverride(volumeId, Guid.CreateVersion7(), ".webp");

    private static ICoverStore StoreHolding(params string[] keys)
    {
        var coverStore = Substitute.For<ICoverStore>();
        coverStore.ListKeysAsync("override/volume/", Arg.Any<CancellationToken>()).Returns(ToAsync(keys));
        coverStore.ListKeysAsync(CoverObjectKey.AvatarPrefix, Arg.Any<CancellationToken>()).Returns(ToAsync([]));

        return coverStore;
    }

    private static async IAsyncEnumerable<string> ToAsync(IEnumerable<string> keys)
    {
        foreach (var key in keys)
        {
            yield return key;
        }

        await Task.CompletedTask;
    }

    // Yields the key, then saves its volume, the way an upload lands just after a volume that
    // did not exist when the sweep began.
    private static async IAsyncEnumerable<string> ListingThenAdding(string key, Func<Task> addVolume)
    {
        yield return key;

        await addVolume();
    }

    private static async Task AddUserAsync(string database, Guid userId, string providerAvatar, string uploadedAvatar)
    {
        await using var dbContext = Context(database, userId);
        dbContext.Users.Add(new User
        {
            Id = userId,
            Issuer = "test",
            Subject = userId.ToString(),
            Email = $"{userId}@example.com",
            ProviderAvatarObjectKey = providerAvatar,
            AvatarOverrideObjectKey = uploadedAvatar,
        });
        await dbContext.SaveChangesAsync();
    }

    private static async Task<Guid> AddVolumeAsync(
        string database,
        Guid userId,
        Guid? id = null,
        string? overrideObjectKey = null)
    {
        await using var dbContext = Context(database, userId);
        var volumeId = id ?? Guid.CreateVersion7();

        if (!await dbContext.Users.AnyAsync(user => user.Id == userId))
        {
            dbContext.Users.Add(new User { Id = userId, Issuer = "test", Subject = userId.ToString(), Email = $"{userId}@example.com" });
        }

        dbContext.Series.Add(new Series
        {
            Id = Guid.CreateVersion7(),
            TitleEnglish = $"Series of {userId}",
            Format = Format.LightNovel,
            PublicationStatus = PublicationStatus.Ongoing,
            Volumes =
            [
                new Volume
                {
                    Id = volumeId,
                    Number = 1,
                    UserState = overrideObjectKey is null
                        ? null
                        : new VolumeUserState { VolumeId = volumeId, CoverOverrideObjectKey = overrideObjectKey },
                },
            ],
        });
        await dbContext.SaveChangesAsync();

        return volumeId;
    }

    // A published pointer at an upload the volume's own state does not name: the sweep keeps
    // what another user can still pick.
    private static async Task AddPublishedCoverAsync(string database, Guid volumeId, string objectKey)
    {
        await using var dbContext = Context(database, TestUsers.Owner);
        dbContext.CommunityCovers.Add(new CommunityCover
        {
            Id = Guid.CreateVersion7(),
            UploaderUserId = TestUsers.Owner,
            SourceVolumeId = volumeId,
            AniListId = 94846,
            Format = Format.LightNovel,
            Number = 1,
            ContentHash = new string('a', 64),
            ObjectKey = objectKey,
            PublishedAt = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync();
    }

    private static LnShelfDbContext Context(string database, Guid? userId) =>
        new(new DbContextOptionsBuilder<LnShelfDbContext>().UseInMemoryDatabase(database).Options, TestUsers.As(userId));
}
