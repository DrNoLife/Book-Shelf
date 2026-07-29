using LnShelf.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace LnShelf.Api.Tests.Features.Import.Services;

/// <summary>
/// Applying an import to the shelf, and the property the whole flow is judged on:
/// running it twice changes nothing (testing.md #3 — the bugs that corrupt data
/// quietly).
/// </summary>
public sealed class ImportShelfWriterTests
{
    private const int AniListId = 85976;
    private static readonly DateTimeOffset AppliedAt = new(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ApplyAsync_ANewSeries_CreatesItWithVolumesAndStates()
    {
        await using var dbContext = CreateDbContext();
        var writer = CreateWriter(dbContext, CreateMetadataSource(volumeCount: 18));
        var counters = new ImportWriteCounters();

        await writer.ApplyAsync(
            AniListId, [CreateGroup([1, 2, 3])], counters, AppliedAt, CancellationToken.None);
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

    [Fact]
    public async Task ApplyAsync_VolumesTheSourceDoesNotKnow_AreAddedAsLocalOnly()
    {
        // The real Tanya and 86 case: AniList publishes no volume count for an
        // ongoing series, so it arrives with none at all.
        await using var dbContext = CreateDbContext();
        var writer = CreateWriter(dbContext, CreateMetadataSource(volumeCount: null));
        var counters = new ImportWriteCounters();

        await writer.ApplyAsync(
            AniListId, [CreateGroup([1, 2, 3])], counters, AppliedAt, CancellationToken.None);
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
        await writer.ApplyAsync(AniListId, [CreateGroup([1, 2, 3])], first, AppliedAt, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var second = new ImportWriteCounters();
        await writer.ApplyAsync(AniListId, [CreateGroup([1, 2, 3])], second, AppliedAt, CancellationToken.None);
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
        await writer.ApplyAsync(AniListId, [group], new ImportWriteCounters(), AppliedAt, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        // The user then reads and buys it.
        var volume = await dbContext.Volumes.Include(candidate => candidate.UserState)
            .SingleAsync(candidate => candidate.Number == 1);
        volume.UserState!.ReadStatus = ReadStatus.Read;
        volume.UserState.Owned = true;
        await dbContext.SaveChangesAsync();

        // Re-importing the same export must not undo that.
        await writer.ApplyAsync(AniListId, [group], new ImportWriteCounters(), AppliedAt, CancellationToken.None);
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
            AppliedAt,
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
            AniListId, [CreateGroup([1, 2, 3])], new ImportWriteCounters(), AppliedAt, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var series = await LoadSeriesAsync(dbContext);

        // Three of eighteen read: being read, not completed.
        series.UserState!.TrackingStatus.ShouldBe(TrackingStatus.Reading);
        series.UserState.StartedAt.ShouldNotBeNull();
        series.UserState.LastReadAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task ApplyAsync_ASeriesTheUserHasDropped_KeepsThatStatus()
    {
        await using var dbContext = CreateDbContext();
        var writer = CreateWriter(dbContext, CreateMetadataSource(volumeCount: 18));

        await writer.ApplyAsync(
            AniListId, [CreateGroup([1])], new ImportWriteCounters(), AppliedAt, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var series = await LoadSeriesAsync(dbContext);
        series.UserState!.TrackingStatus = TrackingStatus.Dropped;
        await dbContext.SaveChangesAsync();

        await writer.ApplyAsync(
            AniListId, [CreateGroup([1, 2])], new ImportWriteCounters(), AppliedAt, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        // The importer promotes from the untouched default only; a status the user
        // set is theirs.
        (await LoadSeriesAsync(dbContext)).UserState!.TrackingStatus.ShouldBe(TrackingStatus.Dropped);
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
            AniListId, [CreateGroup([1])], new ImportWriteCounters(), AppliedAt, CancellationToken.None);

        await act.ShouldThrowAsync<LnShelf.Api.Errors.ConflictException>();
    }

    private static Task<Series> LoadSeriesAsync(LnShelfDbContext dbContext) =>
        dbContext.Series
            .Include(series => series.Volumes).ThenInclude(volume => volume.UserState)
            .Include(series => series.UserState)
            .SingleAsync();

    private static ImportShelfWriter CreateWriter(
        LnShelfDbContext dbContext,
        ISeriesMetadataSource metadataSource) =>
        new(dbContext, metadataSource, Substitute.For<ICoverCache>(),
            NullLogger<ImportShelfWriter>.Instance);

    private static ISeriesMetadataSource CreateMetadataSource(int? volumeCount)
    {
        var metadataSource = Substitute.For<ISeriesMetadataSource>();
        metadataSource.GetSeriesAsync(AniListId, Arg.Any<CancellationToken>())
            .Returns(new SeriesMetadata
            {
                ExternalId = AniListId,
                TitleEnglish = "Overlord",
                Format = Format.LightNovel,
                PublicationStatus = PublicationStatus.Ongoing,
                Volumes = volumeCount is null
                    ? []
                    : [.. Enumerable.Range(1, volumeCount.Value).Select(number => new VolumeMetadata { Number = number })],
            });

        return metadataSource;
    }

    private static ImportGroup CreateGroup(
        int[] volumeNumbers,
        ReadStatus readStatus = ReadStatus.Read,
        bool treatReadAsOwned = true)
    {
        var group = new ImportGroup
        {
            Id = Guid.CreateVersion7(),
            SeriesTitle = "Overlord",
            SeriesKey = "overlord",
            Status = ImportGroupStatus.Resolved,
            MatchedAniListId = AniListId,
            TreatReadAsOwned = treatReadAsOwned,
        };

        foreach (var volumeNumber in volumeNumbers)
        {
            group.Rows.Add(new ImportRow
            {
                Id = Guid.CreateVersion7(),
                LineNumber = volumeNumber,
                RawTitle = $"Overlord, Vol. {volumeNumber}",
                VolumeNumber = volumeNumber,
                ReadStatus = readStatus,
                OwnedInExport = false,
                OwnedFormat = OwnedFormat.Digital,
                ReadAt = new DateOnly(2022, 3, 12),
            });
        }

        return group;
    }

    private static LnShelfDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<LnShelfDbContext>()
            .UseInMemoryDatabase($"import-{Guid.CreateVersion7()}")
            .Options);
}
