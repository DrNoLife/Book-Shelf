using Microsoft.EntityFrameworkCore;

namespace LnShelf.Api.Tests.Features.Shelf.Services;

/// <summary>
/// Folding a phantom volume into the bonus volume it stood for: every user field moves, the
/// uploaded cover is re-keyed to its new volume, and every refusal writes nothing.
/// </summary>
public sealed class VolumeMergeCommandServiceTests
{
    private const string Title = "The Torture Princess: Fremd Torturchen";

    [Fact]
    public async Task MergeAsync_PhantomWithFullUserState_MovesEveryFieldAndDeletesTheSource()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext);
        var source = series.Volumes.Single(volume => volume.Number == 10);
        var target = series.Volumes.Single(volume => volume.Number == 7.5m);
        var oldCoverKey = CoverObjectKey.ForVolumeOverride(source.Id, Guid.CreateVersion7(), ".png");
        source.UserState = FullState(source.Id, oldCoverKey);
        await dbContext.SaveChangesAsync();

        var coverStore = CoverStoreHolding(oldCoverKey);

        var report = await CreateService(dbContext, coverStore).MergeAsync(Request(), CancellationToken.None);

        report.MovedUserState.ShouldBeTrue();
        report.MovedCoverOverride.ShouldBeTrue();
        (await dbContext.Volumes.AnyAsync(volume => volume.Id == source.Id)).ShouldBeFalse();
        (await dbContext.VolumeUserStates.AnyAsync(state => state.VolumeId == source.Id)).ShouldBeFalse();

        var moved = await dbContext.VolumeUserStates.SingleAsync(state => state.VolumeId == target.Id);
        moved.ReadStatus.ShouldBe(ReadStatus.Read);
        moved.ReadAt.ShouldBe(new DateOnly(2024, 2, 3));
        moved.Owned.ShouldBeTrue();
        moved.OwnedFormat.ShouldBe(OwnedFormat.Physical);
        moved.PurchasedAt.ShouldBe(new DateOnly(2024, 1, 20));
        moved.PurchasePrice.ShouldBe(14.99m);
        moved.NotifyWhenDated.ShouldBeTrue();

        // Re-keyed under the target, or the covers endpoint and the orphan sweep would treat it
        // as belonging to the deleted volume.
        var newCoverKey = moved.CoverOverrideObjectKey.ShouldNotBeNull();
        CoverObjectKey.VolumeIdOf(newCoverKey).ShouldBe(target.Id);
        newCoverKey.ShouldEndWith(".png");
        await coverStore.Received(1).SaveAsync(newCoverKey, Arg.Any<ReadOnlyMemory<byte>>(), "image/png", Arg.Any<CancellationToken>());
        await coverStore.Received(1).DeleteAsync(oldCoverKey, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MergeAsync_TargetSyncedMetadata_IsLeftAlone()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext);
        var source = series.Volumes.Single(volume => volume.Number == 10);
        source.UserState = new VolumeUserState { VolumeId = source.Id, ReadStatus = ReadStatus.Read };
        await dbContext.SaveChangesAsync();

        await CreateService(dbContext).MergeAsync(Request(), CancellationToken.None);

        var target = await dbContext.Volumes.SingleAsync(volume => volume.Number == 7.5m);
        target.ReleaseDate.ShouldBe(new DateOnly(2023, 6, 1));
        target.ExternalId.ShouldBe("anilist-7.5");
        target.CoverObjectKey.ShouldBe("volume/openlibrary/synced.jpg");
    }

    [Fact]
    public async Task MergeAsync_TargetWithAnUntouchedStateRow_IsMergedInto()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext);
        var source = series.Volumes.Single(volume => volume.Number == 10);
        var target = series.Volumes.Single(volume => volume.Number == 7.5m);
        source.UserState = new VolumeUserState { VolumeId = source.Id, Owned = true, OwnedFormat = OwnedFormat.Digital };
        target.UserState = new VolumeUserState { VolumeId = target.Id };
        await dbContext.SaveChangesAsync();

        await CreateService(dbContext).MergeAsync(Request(), CancellationToken.None);

        var moved = await dbContext.VolumeUserStates.SingleAsync(state => state.VolumeId == target.Id);
        moved.Owned.ShouldBeTrue();
        moved.OwnedFormat.ShouldBe(OwnedFormat.Digital);
    }

    public static TheoryData<VolumeUserState> MeaningfulTargetStates => new()
    {
        new VolumeUserState { ReadStatus = ReadStatus.Reading },
        new VolumeUserState { ReadAt = new DateOnly(2024, 5, 1) },
        new VolumeUserState { Owned = true },
        new VolumeUserState { OwnedFormat = OwnedFormat.Physical },
        new VolumeUserState { PurchasedAt = new DateOnly(2024, 5, 1) },
        new VolumeUserState { PurchasePrice = 9.99m },
        new VolumeUserState { NotifyWhenDated = true },
        new VolumeUserState { CoverOverrideObjectKey = "override/volume/x/y.jpg" },
    };

    [Theory]
    [MemberData(nameof(MeaningfulTargetStates))]
    public async Task MergeAsync_TargetWithUserState_IsRefusedAndWritesNothing(VolumeUserState targetState)
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext);
        var source = series.Volumes.Single(volume => volume.Number == 10);
        var target = series.Volumes.Single(volume => volume.Number == 7.5m);
        source.UserState = new VolumeUserState { VolumeId = source.Id, ReadStatus = ReadStatus.Read };
        targetState.VolumeId = target.Id;
        target.UserState = targetState;
        await dbContext.SaveChangesAsync();

        await Should.ThrowAsync<InvalidInputException>(
            () => CreateService(dbContext).MergeAsync(Request(), CancellationToken.None));

        await ShouldBeUnchangedAsync(dbContext, source.Id);
    }

    [Theory]
    [InlineData(11, 7.5)]
    [InlineData(10, 8.5)]
    [InlineData(10, 10)]
    public async Task MergeAsync_MissingOrSameVolume_IsRefusedAndWritesNothing(decimal fromNumber, decimal intoNumber)
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext);
        var source = series.Volumes.Single(volume => volume.Number == 10);

        await Should.ThrowAsync<InvalidInputException>(() => CreateService(dbContext).MergeAsync(
            Request() with { FromNumber = fromNumber, IntoNumber = intoNumber }, CancellationToken.None));

        (await dbContext.Volumes.CountAsync()).ShouldBe(3);
        (await dbContext.Volumes.AnyAsync(volume => volume.Id == source.Id)).ShouldBeTrue();
    }

    [Fact]
    public async Task MergeAsync_UnknownTitle_IsRefused()
    {
        await using var dbContext = CreateDbContext();
        await SeedAsync(dbContext);

        await Should.ThrowAsync<InvalidInputException>(() => CreateService(dbContext).MergeAsync(
            Request() with { SeriesTitle = "Some Other Series" }, CancellationToken.None));
    }

    [Fact]
    public async Task MergeAsync_TitleSharedByTheMangaWithoutFormat_IsRefusedAndWithFormatMerges()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext);
        dbContext.Series.Add(new Series { Id = Guid.CreateVersion7(), TitleEnglish = Title, Format = Format.Manga });
        await dbContext.SaveChangesAsync();

        await Should.ThrowAsync<InvalidInputException>(
            () => CreateService(dbContext).MergeAsync(Request(), CancellationToken.None));

        await CreateService(dbContext).MergeAsync(Request() with { Format = Format.LightNovel }, CancellationToken.None);
        (await dbContext.Volumes.CountAsync(volume => volume.SeriesId == series.Id)).ShouldBe(2);
    }

    [Fact]
    public async Task MergeAsync_DryRun_ReportsAndChangesNothing()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext);
        var source = series.Volumes.Single(volume => volume.Number == 10);
        var oldCoverKey = CoverObjectKey.ForVolumeOverride(source.Id, Guid.CreateVersion7(), ".jpg");
        source.UserState = FullState(source.Id, oldCoverKey);
        await dbContext.SaveChangesAsync();
        var coverStore = CoverStoreHolding(oldCoverKey);

        var report = await CreateService(dbContext, coverStore).MergeAsync(
            Request() with { DryRun = true }, CancellationToken.None);

        report.DryRun.ShouldBeTrue();
        report.MovedUserState.ShouldBeTrue();
        report.Summary.ShouldStartWith("Would merge");
        await ShouldBeUnchangedAsync(dbContext, source.Id);
        (await dbContext.VolumeUserStates.SingleAsync()).CoverOverrideObjectKey.ShouldBe(oldCoverKey);
        await coverStore.DidNotReceiveWithAnyArgs().SaveAsync(default!, default, default!, default);
        await coverStore.DidNotReceiveWithAnyArgs().DeleteAsync(default!, default);
    }

    [Fact]
    public async Task MergeAsync_SourceChoiceOnTheSourceVolume_IsRefusedAndWritesNothing()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext);
        var source = series.Volumes.Single(volume => volume.Number == 10);
        source.UserState = new VolumeUserState { VolumeId = source.Id, ReadStatus = ReadStatus.Read };
        series.SourceChoices.Add(new SourceChoice
        {
            Id = Guid.CreateVersion7(),
            Field = SyncDiscrepancyField.ReleaseDate,
            VolumeNumber = 10,
            Source = MetadataSourceName.OpenLibrary,
            ChosenAt = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync();

        await Should.ThrowAsync<InvalidInputException>(
            () => CreateService(dbContext).MergeAsync(Request(), CancellationToken.None));

        await ShouldBeUnchangedAsync(dbContext, source.Id);
    }

    [Fact]
    public async Task MergeAsync_TheReleaseSourceStillCountsTheSourceVolume_IsRefusedAndWritesNothing()
    {
        // Sync recreates every volume up to the count, so merging volume 10 away while the
        // count is still 10 would only last until the next night.
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext);
        series.ReleaseSourceVolumeCount = 10;
        var source = series.Volumes.Single(volume => volume.Number == 10);
        source.UserState = new VolumeUserState { VolumeId = source.Id, ReadStatus = ReadStatus.Read };
        await dbContext.SaveChangesAsync();

        await Should.ThrowAsync<InvalidInputException>(
            () => CreateService(dbContext).MergeAsync(Request(), CancellationToken.None));

        await ShouldBeUnchangedAsync(dbContext, source.Id);
    }

    [Fact]
    public async Task MergeAsync_CoverOverrideMissingFromTheStore_IsRefusedAndWritesNothing()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext);
        var source = series.Volumes.Single(volume => volume.Number == 10);
        source.UserState = FullState(source.Id, CoverObjectKey.ForVolumeOverride(source.Id, Guid.CreateVersion7(), ".jpg"));
        await dbContext.SaveChangesAsync();

        await Should.ThrowAsync<InvalidInputException>(
            () => CreateService(dbContext).MergeAsync(Request(), CancellationToken.None));

        await ShouldBeUnchangedAsync(dbContext, source.Id);
    }

    [Fact]
    public async Task MergeAsync_CoverOverrideMoved_StagesTheTargetWithTheCopiedKeyAndHash()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext);
        var source = series.Volumes.Single(volume => volume.Number == 10);
        var target = series.Volumes.Single(volume => volume.Number == 7.5m);
        var oldCoverKey = CoverObjectKey.ForVolumeOverride(source.Id, Guid.CreateVersion7(), ".png");
        source.UserState = FullState(source.Id, oldCoverKey);
        await dbContext.SaveChangesAsync();

        var communityCoverCommand = Substitute.For<ICommunityCoverCommandService>();

        await CreateService(dbContext, CoverStoreHolding(oldCoverKey), communityCoverCommand)
            .MergeAsync(Request(), CancellationToken.None);

        var newCoverKey = (await dbContext.VolumeUserStates.SingleAsync(state => state.VolumeId == target.Id))
            .CoverOverrideObjectKey.ShouldNotBeNull();
        await communityCoverCommand.Received(1).StageForVolumeAsync(
            series, target, newCoverKey, CoverContentHash.Compute([1, 2, 3]), Arg.Any<CancellationToken>());
    }

    private static VolumeMergeRequest Request() => new(Title, Format: null, FromNumber: 10, IntoNumber: 7.5m, DryRun: false);

    private static VolumeUserState FullState(Guid volumeId, string coverKey) => new()
    {
        VolumeId = volumeId,
        ReadStatus = ReadStatus.Read,
        ReadAt = new DateOnly(2024, 2, 3),
        Owned = true,
        OwnedFormat = OwnedFormat.Physical,
        PurchasedAt = new DateOnly(2024, 1, 20),
        PurchasePrice = 14.99m,
        NotifyWhenDated = true,
        CoverOverrideObjectKey = coverKey,
    };

    private static ICoverStore CoverStoreHolding(string objectKey)
    {
        var coverStore = Substitute.For<ICoverStore>();
        coverStore.ExistsAsync(objectKey, Arg.Any<CancellationToken>()).Returns(true);
        coverStore.ReadAsync(objectKey, Arg.Any<CancellationToken>())
            .Returns(new CoverContent([1, 2, 3], "image/png"));
        return coverStore;
    }

    private static async Task ShouldBeUnchangedAsync(LnShelfDbContext dbContext, Guid sourceId)
    {
        (await dbContext.Volumes.CountAsync()).ShouldBe(3);
        (await dbContext.Volumes.AnyAsync(volume => volume.Id == sourceId)).ShouldBeTrue();
        (await dbContext.VolumeUserStates.AnyAsync(state => state.VolumeId == sourceId)).ShouldBeTrue();
    }

    private static VolumeMergeCommandService CreateService(
        LnShelfDbContext dbContext,
        ICoverStore? coverStore = null,
        ICommunityCoverCommandService? communityCoverCommand = null) =>
        new(dbContext,
            coverStore ?? Substitute.For<ICoverStore>(),
            communityCoverCommand ?? Substitute.For<ICommunityCoverCommandService>(),
            NullLogger<VolumeMergeCommandService>.Instance);

    /// <summary>Volumes 7, 7.5 (the real bonus volume, synced) and 10 (the phantom).</summary>
    private static async Task<Series> SeedAsync(LnShelfDbContext dbContext)
    {
        var series = new Series
        {
            Id = Guid.CreateVersion7(),
            TitleEnglish = Title,
            TitleRomaji = "Isekai Goumon Hime",
            Format = Format.LightNovel,
            PublicationStatus = PublicationStatus.Ongoing,
        };

        series.Volumes.Add(new Volume { Id = Guid.CreateVersion7(), Number = 7, IsReleased = true, ExternalId = "anilist-7" });
        series.Volumes.Add(new Volume
        {
            Id = Guid.CreateVersion7(),
            Number = 7.5m,
            IsReleased = true,
            ReleaseDate = new DateOnly(2023, 6, 1),
            ExternalId = "anilist-7.5",
            CoverObjectKey = "volume/openlibrary/synced.jpg",
        });
        series.Volumes.Add(new Volume { Id = Guid.CreateVersion7(), Number = 10, IsReleased = true, ExternalId = "anilist-10" });

        dbContext.Series.Add(series);
        await dbContext.SaveChangesAsync();
        return series;
    }

    private static LnShelfDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<LnShelfDbContext>()
            .UseInMemoryDatabase($"volume-merge-{Guid.CreateVersion7()}")
            .Options, TestUsers.Current);
}
