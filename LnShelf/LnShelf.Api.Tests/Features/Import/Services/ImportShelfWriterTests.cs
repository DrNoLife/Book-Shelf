using Microsoft.EntityFrameworkCore;

using static LnShelf.Api.Tests.Features.Import.Fixtures.ImportShelfWriterHarness;

namespace LnShelf.Api.Tests.Features.Import.Services;

/// <summary>
/// Applying an import to the shelf, and the property the whole flow is judged on:
/// running it twice changes nothing (testing.md #3 — the bugs that corrupt data
/// quietly).
/// </summary>
public sealed class ImportShelfWriterTests
{
    [Fact]
    public async Task ApplyAsync_ANewSeries_CreatesItWithVolumesAndStates()
    {
        await using var dbContext = CreateDbContext();
        var writer = CreateWriter(dbContext, CreateMetadataSource(volumeCount: 18));
        var counters = new ImportWriteCounters();

        await writer.ApplyAsync(
            AniListId, [CreateGroup([1, 2, 3])], counters, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        counters.SeriesCreated.ShouldBe(1);
        counters.SeriesUpdated.ShouldBe(0);
        counters.VolumeStatesWritten.ShouldBe(3);

        // AniList said 18 volumes, so all 18 exist and none of them is local-only.
        counters.LocalOnlyVolumesCreated.ShouldBe(0);

        var series = await LoadSeriesAsync(dbContext);
        series.AniListId.ShouldBe(AniListId);
        series.Volumes.Count.ShouldBe(18);
        series.Volumes.Count(volume => volume.UserState is not null).ShouldBe(3);
        series.Volumes.Where(volume => volume.Number <= 3)
            .ShouldAllBe(volume => volume.UserState!.ReadStatus == ReadStatus.Read && volume.UserState.Owned);
    }

    [Theory]
    [InlineData(Format.Manga, ProgressUnit.Chapters, ProgressUnit.Chapters)]
    [InlineData(Format.LightNovel, ProgressUnit.Chapters, ProgressUnit.Volumes)]
    public async Task ApplyAsync_ANewSeries_StartsInThePreferredUnitWhereTheFormatOffersChapters(
        Format format,
        ProgressUnit preferred,
        ProgressUnit expected)
    {
        await using var dbContext = CreateDbContext();
        await TestPreferences.ChooseDefaultProgressUnitAsync(dbContext, preferred);
        var writer = CreateWriter(dbContext, CreateMetadataSource(volumeCount: 1, format));

        await writer.ApplyAsync(
            AniListId, [CreateGroup([1])], new ImportWriteCounters(), CancellationToken.None);
        await dbContext.SaveChangesAsync();

        (await LoadSeriesAsync(dbContext)).UserState.ShouldNotBeNull().ProgressUnit.ShouldBe(expected);
    }

    [Fact]
    public async Task ApplyAsync_AReImportAfterThePreferenceChanged_LeavesTheSeriesUnitAlone()
    {
        await using var dbContext = CreateDbContext();
        var writer = CreateWriter(dbContext, CreateMetadataSource(volumeCount: 1, Format.Manga));

        await writer.ApplyAsync(
            AniListId, [CreateGroup([1])], new ImportWriteCounters(), CancellationToken.None);
        await dbContext.SaveChangesAsync();

        await TestPreferences.ChooseDefaultProgressUnitAsync(dbContext, ProgressUnit.Chapters);

        await writer.ApplyAsync(
            AniListId, [CreateGroup([1])], new ImportWriteCounters(), CancellationToken.None);
        await dbContext.SaveChangesAsync();

        (await LoadSeriesAsync(dbContext)).UserState.ShouldNotBeNull().ProgressUnit.ShouldBe(ProgressUnit.Volumes);
    }

    [Fact]
    public async Task ApplyAsync_VolumesTheSourceDoesNotKnow_AreAddedAsLocalOnly()
    {
        // The real Tanya and 86 case: AniList publishes no volume count for an
        // ongoing series, so it arrives with none at all.
        await using var dbContext = CreateDbContext();
        var writer = CreateWriter(dbContext, CreateMetadataSource(volumeCount: null));
        var counters = new ImportWriteCounters();

        await writer.ApplyAsync(
            AniListId, [CreateGroup([1, 2, 3])], counters, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        counters.LocalOnlyVolumesCreated.ShouldBe(3);

        var series = await LoadSeriesAsync(dbContext);
        series.Volumes.Count.ShouldBe(3);

        // Flagged so a later sync reconciles them rather than overwriting them.
        series.Volumes.ShouldAllBe(volume => volume.IsLocalOnly);
    }

    [Fact]
    public async Task ApplyAsync_TheSameImportTwice_CreatesNothingTheSecondTime()
    {
        await using var dbContext = CreateDbContext();
        var metadataSource = CreateMetadataSource(volumeCount: 18);
        var writer = CreateWriter(dbContext, metadataSource);

        var first = new ImportWriteCounters();
        await writer.ApplyAsync(AniListId, [CreateGroup([1, 2, 3])], first, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var second = new ImportWriteCounters();
        await writer.ApplyAsync(AniListId, [CreateGroup([1, 2, 3])], second, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        first.SeriesCreated.ShouldBe(1);
        second.SeriesCreated.ShouldBe(0);
        second.SeriesUpdated.ShouldBe(1);
        second.LocalOnlyVolumesCreated.ShouldBe(0);

        (await dbContext.Series.CountAsync()).ShouldBe(1);
        (await dbContext.Volumes.CountAsync()).ShouldBe(18);
        (await dbContext.VolumeUserStates.CountAsync()).ShouldBe(3);

        // And the second run asked the metadata source nothing at all.
        await metadataSource.Received(1).GetSeriesAsync(AniListId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApplyAsync_AVolumeTheUserHasSinceRead_IsNotDowngraded()
    {
        await using var dbContext = CreateDbContext();
        var writer = CreateWriter(dbContext, CreateMetadataSource(volumeCount: 18));

        // Import once as unread-and-unowned.
        var group = CreateGroup([1], ReadStatus.Unread, treatReadAsOwned: false);
        await writer.ApplyAsync(AniListId, [group], new ImportWriteCounters(), CancellationToken.None);
        await dbContext.SaveChangesAsync();

        // The user then reads and buys it.
        var volume = await dbContext.Volumes.Include(candidate => candidate.UserState)
            .SingleAsync(candidate => candidate.Number == 1);
        volume.UserState!.ReadStatus = ReadStatus.Read;
        volume.UserState.Owned = true;
        await dbContext.SaveChangesAsync();

        // Re-importing the same export must not undo that.
        await writer.ApplyAsync(AniListId, [group], new ImportWriteCounters(), CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var reloaded = await dbContext.Volumes.Include(candidate => candidate.UserState)
            .SingleAsync(candidate => candidate.Number == 1);

        reloaded.UserState!.ReadStatus.ShouldBe(ReadStatus.Read);
        reloaded.UserState.Owned.ShouldBeTrue();
    }

    [Fact]
    public async Task ApplyAsync_TwoGroupsResolvingToOneSeries_BecomeOneShelfEntry()
    {
        // The real Overlord case: English volumes and Japanese volumes, one series.
        await using var dbContext = CreateDbContext();
        var writer = CreateWriter(dbContext, CreateMetadataSource(volumeCount: 18));
        var counters = new ImportWriteCounters();

        await writer.ApplyAsync(
            AniListId,
            [CreateGroup([2, 3, 4, 5, 6, 7, 8, 9, 15]), CreateGroup([1, 10, 11, 12, 13, 14])],
            counters,
            CancellationToken.None);
        await dbContext.SaveChangesAsync();

        counters.SeriesCreated.ShouldBe(1);
        counters.VolumeStatesWritten.ShouldBe(15);

        var series = await LoadSeriesAsync(dbContext);
        series.Volumes.Count(volume => volume.UserState is not null).ShouldBe(15);
    }

    [Fact]
    public async Task ApplyAsync_AnUntouchedSeries_GetsATrackingStatusFromWhatWasImported()
    {
        await using var dbContext = CreateDbContext();
        var writer = CreateWriter(dbContext, CreateMetadataSource(volumeCount: 18));

        await writer.ApplyAsync(
            AniListId, [CreateGroup([1, 2, 3])], new ImportWriteCounters(), CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var series = await LoadSeriesAsync(dbContext);

        // Three of eighteen read: being read, not completed.
        series.UserState!.TrackingStatus.ShouldBe(TrackingStatus.Reading);
        series.UserState.StartedAt.ShouldNotBeNull();
        series.UserState.LastReadAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task ApplyAsync_NothingButToReadRows_LeavesTheSeriesPlanToStart()
    {
        await using var dbContext = CreateDbContext();
        var writer = CreateWriter(dbContext, CreateMetadataSource(volumeCount: 18));

        await writer.ApplyAsync(
            AniListId,
            [CreateGroup([1, 2, 3], ReadStatus.Unread, treatReadAsOwned: false)],
            new ImportWriteCounters(),
            CancellationToken.None);
        await dbContext.SaveChangesAsync();

        // A wish list is not evidence of reading.
        (await LoadSeriesAsync(dbContext)).UserState!.TrackingStatus.ShouldBe(TrackingStatus.PlanToStart);
    }

    /// <summary>
    /// A series added from MangaUpdates while AniList was down has no AniList id, so the
    /// import's own lookup cannot see it — and used to create a second copy beside it.
    /// </summary>
    [Fact]
    public async Task ApplyAsync_ASeriesAddedFromMangaUpdatesUnderTheSameTitle_ImportsOntoIt()
    {
        await using var dbContext = CreateDbContext();
        var existing = new Series
        {
            Id = Guid.CreateVersion7(),
            MangaUpdatesId = 17992364412,
            TitleEnglish = "Overlord",
            Format = Format.LightNovel,
            PublicationStatus = PublicationStatus.Ongoing,
        };
        existing.Volumes.Add(new Volume { Id = Guid.CreateVersion7(), Number = 1, IsReleased = true });
        dbContext.Series.Add(existing);
        await dbContext.SaveChangesAsync();

        var counters = new ImportWriteCounters();
        await CreateWriter(dbContext, CreateMetadataSource(volumeCount: 18)).ApplyAsync(
            AniListId, [CreateGroup([1])], counters, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        counters.SeriesCreated.ShouldBe(0);
        counters.SeriesUpdated.ShouldBe(1);

        var series = await LoadSeriesAsync(dbContext);
        series.Id.ShouldBe(existing.Id);
        series.AniListId.ShouldBe(AniListId);
        series.Volumes.Single().UserState.ShouldNotBeNull().ReadStatus.ShouldBe(ReadStatus.Read);
    }

    [Fact]
    public async Task ApplyAsync_ASeriesTheUserHasDropped_KeepsThatStatus()
    {
        await using var dbContext = CreateDbContext();
        var writer = CreateWriter(dbContext, CreateMetadataSource(volumeCount: 18));

        await writer.ApplyAsync(
            AniListId, [CreateGroup([1])], new ImportWriteCounters(), CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var series = await LoadSeriesAsync(dbContext);
        series.UserState!.TrackingStatus = TrackingStatus.Dropped;
        await dbContext.SaveChangesAsync();

        await writer.ApplyAsync(
            AniListId, [CreateGroup([1, 2])], new ImportWriteCounters(), CancellationToken.None);
        await dbContext.SaveChangesAsync();

        // The importer promotes from the untouched default only; a status the user
        // set is theirs.
        (await LoadSeriesAsync(dbContext)).UserState!.TrackingStatus.ShouldBe(TrackingStatus.Dropped);
    }

    [Fact]
    public async Task ApplyAsync_RowsCarryingAnIsbn_RecordItOnTheVolume()
    {
        await using var dbContext = CreateDbContext();
        var writer = CreateWriter(dbContext, CreateMetadataSource(volumeCount: 18));

        await writer.ApplyAsync(
            AniListId,
            [CreateGroup([3], isbn: "978-0-316-36398-3")],
            new ImportWriteCounters(),
            CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var volume = await dbContext.Volumes.SingleAsync(candidate => candidate.Number == 3);
        volume.Isbn.ShouldBe("9780316363983");

        // Only the volume the row named. The other 17 came from a volume count, which
        // says nothing about editions.
        (await dbContext.Volumes.CountAsync(candidate => candidate.Isbn != null)).ShouldBe(1);
    }

    [Fact]
    public async Task ApplyAsync_ASecondImportWithoutIsbns_KeepsTheOneAlreadyRecorded()
    {
        await using var dbContext = CreateDbContext();
        var writer = CreateWriter(dbContext, CreateMetadataSource(volumeCount: 18));

        await writer.ApplyAsync(
            AniListId,
            [CreateGroup([3], isbn: "9780316363983")],
            new ImportWriteCounters(),
            CancellationToken.None);
        await dbContext.SaveChangesAsync();

        // The same export re-cut without the ISBN column, or a different export
        // covering the same volume. Neither is grounds for erasing the identifier.
        await writer.ApplyAsync(
            AniListId, [CreateGroup([3])], new ImportWriteCounters(), CancellationToken.None);
        await dbContext.SaveChangesAsync();

        (await dbContext.Volumes.SingleAsync(candidate => candidate.Number == 3))
            .Isbn.ShouldBe("9780316363983");
    }

    [Fact]
    public async Task ApplyAsync_ASecondImportNamingAnotherEdition_DoesNotReplaceTheFirst()
    {
        await using var dbContext = CreateDbContext();
        var writer = CreateWriter(dbContext, CreateMetadataSource(volumeCount: 18));

        await writer.ApplyAsync(
            AniListId,
            [CreateGroup([3], isbn: "9780316363983")],
            new ImportWriteCounters(),
            CancellationToken.None);
        await dbContext.SaveChangesAsync();

        await writer.ApplyAsync(
            AniListId,
            [CreateGroup([3], isbn: "9784041052853")],
            new ImportWriteCounters(),
            CancellationToken.None);
        await dbContext.SaveChangesAsync();

        // First one wins. Two editions of one volume is not a conflict worth churning
        // a lookup key over, and the shelf tracks one edition per volume.
        (await dbContext.Volumes.SingleAsync(candidate => candidate.Number == 3))
            .Isbn.ShouldBe("9780316363983");
    }

    [Fact]
    public async Task ApplyAsync_ARichlyStatedVolumeReimported_LosesNothing()
    {
        await using var dbContext = CreateDbContext();
        var writer = CreateWriter(dbContext, CreateMetadataSource(volumeCount: 18));

        await writer.ApplyAsync(
            AniListId,
            [CreateGroup([3], subtitle: "The Bloody Valkyrie", isbn: "9780316363983")],
            new ImportWriteCounters(),
            CancellationToken.None);
        await dbContext.SaveChangesAsync();

        // A weaker second export of the same volume: not read, not owned, no date, no
        // ISBN, no subtitle. Every one of those is a gap in the file, not an instruction.
        var weaker = CreateGroup([3], ReadStatus.Unread, treatReadAsOwned: false);
        weaker.Rows.Single().ReadAt = null;
        weaker.Rows.Single().OwnedFormat = null;

        var counters = new ImportWriteCounters();
        await writer.ApplyAsync(AniListId, [weaker], counters, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var volume = await LoadVolumeAsync(dbContext, 3);

        volume.UserState!.ReadStatus.ShouldBe(ReadStatus.Read);
        volume.UserState.Owned.ShouldBeTrue();
        volume.UserState.OwnedFormat.ShouldBe(OwnedFormat.Digital);
        volume.UserState.ReadAt.ShouldBe(DefaultReadAt);
        volume.Isbn.ShouldBe("9780316363983");
        volume.Title.ShouldBe("The Bloody Valkyrie");

        // Nothing new was created either — the second run found everything.
        counters.SeriesCreated.ShouldBe(0);
        counters.LocalOnlyVolumesCreated.ShouldBe(0);
        counters.VolumeSubtitlesWritten.ShouldBe(0);
    }

    [Fact]
    public async Task ApplyAsync_TheSourceHasForgottenTheSeries_Throws()
    {
        await using var dbContext = CreateDbContext();
        var metadataSource = Substitute.For<ISeriesMetadataSource>();
        metadataSource.GetSeriesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((SeriesMetadata?)null);

        var writer = CreateWriter(dbContext, metadataSource);

        var act = async () => await writer.ApplyAsync(
            AniListId, [CreateGroup([1])], new ImportWriteCounters(), CancellationToken.None);

        await act.ShouldThrowAsync<LnShelf.Api.Errors.ConflictException>();
    }
}
