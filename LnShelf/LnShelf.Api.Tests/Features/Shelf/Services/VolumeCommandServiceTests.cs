using Microsoft.EntityFrameworkCore;

namespace LnShelf.Api.Tests.Features.Shelf.Services;

/// <summary>
/// The per-volume writes behind the tile: the read cycle, the owned toggle, and the
/// bulk "mark 1–N" an imported collection needs.
/// </summary>
// Every write answers with the whole series, so the assertions about one volume read it
// back out of that response. That is the point of the shape: the counts the hero renders
// come from the same round trip as the tile, and cannot disagree with it.
public sealed class VolumeCommandServiceTests
{
    [Fact]
    public async Task SetReadAsync_AVolumeWithNoStateRow_CreatesOneAndStampsTheDate()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, volumeCount: 3);
        var volume = series.Volumes.Single(candidate => candidate.Number == 1);

        var detail = await CreateService(dbContext).SetReadAsync(
            volume.Id, new SetVolumeReadRequest { Status = ReadStatus.Read }, CancellationToken.None);

        var response = Written(detail, volume.Id);
        response.ReadStatus.ShouldBe(ReadStatus.Read);
        response.ReadAt.ShouldNotBeNull();

        // Absence of a row means Unread and NotOwned; touching it is what creates one.
        (await dbContext.VolumeUserStates.CountAsync()).ShouldBe(1);
    }

    /// <summary>
    /// The whole reason the single-volume writes answer with the series: the counts the
    /// hero renders are refreshed by the same round trip that moved the tile.
    /// </summary>
    // The client used to recompute these from its own copy of the projection's rule, which
    // is the duplication CLAUDE.md's "derived states are computed in queries" forbids.
    [Fact]
    public async Task SetReadAsync_OneVolume_AnswersWithTheSeriesAndItsRefreshedCounts()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, volumeCount: 4);

        var detail = await CreateService(dbContext).SetReadAsync(
            series.Volumes.Single(candidate => candidate.Number == 2).Id,
            new SetVolumeReadRequest { Status = ReadStatus.Read },
            CancellationToken.None);

        detail.ShouldNotBeNull();
        detail.SeriesId.ShouldBe(series.Id);
        detail.Volumes.Count.ShouldBe(4);
        detail.Counts.Read.ShouldBe(1);

        // Every volume is in print and none is owned, so the whole series is still to buy
        // and the CTA still names the first one.
        detail.Counts.ToBuy.ShouldBe(4);
        detail.Counts.NextToBuyNumber.ShouldBe(1);
    }

    [Fact]
    public async Task SetOwnedAsync_OneVolume_AnswersWithTheSeriesAndItsRefreshedCounts()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, volumeCount: 3);

        var detail = await CreateService(dbContext).SetOwnedAsync(
            series.Volumes.Single(candidate => candidate.Number == 1).Id,
            new SetVolumeOwnedRequest { Owned = true },
            CancellationToken.None);

        detail.ShouldNotBeNull();
        detail.Counts.Owned.ShouldBe(1);

        // Owned and unread is backlog; the volume also leaves the to-buy list, so the CTA
        // moves on to the next one.
        detail.Counts.Backlog.ShouldBe(1);
        detail.Counts.NextToBuyNumber.ShouldBe(2);
    }

    [Fact]
    public async Task SetReadAsync_BackToUnread_ClearsTheReadDate()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, volumeCount: 1);
        var volume = series.Volumes.Single();
        var service = CreateService(dbContext);

        await service.SetReadAsync(
            volume.Id, new SetVolumeReadRequest { Status = ReadStatus.Read }, CancellationToken.None);

        var detail = await service.SetReadAsync(
            volume.Id, new SetVolumeReadRequest { Status = ReadStatus.Unread }, CancellationToken.None);

        // A read date on an unread volume is a lie the stats page would later believe.
        Written(detail, volume.Id).ReadAt.ShouldBeNull();
    }

    [Fact]
    public async Task SetReadAsync_AVolumeThatIsOwnedButUndated_IsStillOut()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, volumeCount: 1);
        var volume = series.Volumes.Single();
        var service = CreateService(dbContext);

        await service.SetOwnedAsync(
            volume.Id, new SetVolumeOwnedRequest { Owned = true }, CancellationToken.None);

        var detail = await service.SetReadAsync(
            volume.Id, new SetVolumeReadRequest { Status = ReadStatus.Reading }, CancellationToken.None);

        // The tile needs this: a reading tile has to be one of the four in-print states,
        // never a hatched "not out" one.
        Written(detail, volume.Id).IsOut.ShouldBeTrue();
    }

    [Fact]
    public async Task SetOwnedAsync_WithNoFormatGiven_DefaultsToPhysical()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, volumeCount: 1);
        var volumeId = series.Volumes.Single().Id;

        var detail = await CreateService(dbContext).SetOwnedAsync(
            volumeId, new SetVolumeOwnedRequest { Owned = true }, CancellationToken.None);

        var response = Written(detail, volumeId);
        response.Owned.ShouldBeTrue();
        response.OwnedFormat.ShouldBe(OwnedFormat.Physical);
    }

    [Fact]
    public async Task SetOwnedAsync_UnOwning_KeepsThePriceButDropsTheFormat()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, volumeCount: 1);
        var volumeId = series.Volumes.Single().Id;
        var service = CreateService(dbContext);

        await service.SetOwnedAsync(
            volumeId,
            new SetVolumeOwnedRequest { Owned = true, Format = OwnedFormat.Digital, PurchasePrice = 111m },
            CancellationToken.None);

        var detail = await service.SetOwnedAsync(
            volumeId, new SetVolumeOwnedRequest { Owned = false }, CancellationToken.None);

        var response = Written(detail, volumeId);
        response.Owned.ShouldBeFalse();
        response.OwnedFormat.ShouldBeNull();

        // Selling a volume does not unspend the money.
        (await dbContext.VolumeUserStates.SingleAsync()).PurchasePrice.ShouldBe(111m);
    }

    [Fact]
    public async Task SetOwnedAsync_ANegativePrice_IsRefused()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, volumeCount: 1);

        var act = async () => await CreateService(dbContext).SetOwnedAsync(
            series.Volumes.Single().Id,
            new SetVolumeOwnedRequest { Owned = true, PurchasePrice = -1m },
            CancellationToken.None);

        await act.ShouldThrowAsync<InvalidInputException>();
    }

    [Fact]
    public async Task SetOwnedAsync_APriceTheColumnCannotHold_IsRefused()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, volumeCount: 1);

        var act = async () => await CreateService(dbContext).SetOwnedAsync(
            series.Volumes.Single().Id,
            new SetVolumeOwnedRequest { Owned = true, PurchasePrice = VolumeUserState.MaximumPurchasePrice + 0.01m },
            CancellationToken.None);

        await act.ShouldThrowAsync<InvalidInputException>();
    }

    [Fact]
    public async Task SetOwnedAsync_TheLargestPriceTheColumnHolds_IsStored()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, volumeCount: 1);

        await CreateService(dbContext).SetOwnedAsync(
            series.Volumes.Single().Id,
            new SetVolumeOwnedRequest { Owned = true, PurchasePrice = VolumeUserState.MaximumPurchasePrice },
            CancellationToken.None);

        (await dbContext.VolumeUserStates.SingleAsync()).PurchasePrice.ShouldBe(VolumeUserState.MaximumPurchasePrice);
    }

    [Fact]
    public async Task SetBulkAsync_MarkingVolumesOwned_StampsThePurchaseDateLikeTheSinglePath()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, volumeCount: 3);

        await CreateService(dbContext).SetBulkAsync(
            series.Id,
            new BulkVolumeStateRequest { ThroughNumber = 3, Owned = true },
            CancellationToken.None);

        (await dbContext.VolumeUserStates.ToListAsync())
            .ShouldAllBe(state => state.PurchasedAt == DateOnly.FromDateTime(DateTime.Now.Date));
    }

    [Fact]
    public async Task SetBulkAsync_OwnedAsDigital_OwnsTheRunInThatEdition()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, volumeCount: 4);

        var detail = await CreateService(dbContext).SetBulkAsync(
            series.Id,
            new BulkVolumeStateRequest { ThroughNumber = 3, Owned = true, Format = OwnedFormat.Digital },
            CancellationToken.None);

        detail.ShouldNotBeNull();
        detail.Volumes.Where(volume => volume.Number <= 3)
            .ShouldAllBe(volume => volume.Owned && volume.OwnedFormat == OwnedFormat.Digital);
        detail.Volumes.Single(volume => volume.Number == 4).Owned.ShouldBeFalse();
    }

    /// <summary>
    /// The point of the format on a bulk mark: a run already owned as physical becomes
    /// digital, rather than the bulk write politely keeping what it was told to change.
    /// </summary>
    [Fact]
    public async Task SetBulkAsync_OwnedAsDigital_ReformatsVolumesAlreadyOwned()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, volumeCount: 3);
        var service = CreateService(dbContext);

        await service.SetBulkAsync(
            series.Id, new BulkVolumeStateRequest { ThroughNumber = 3, Owned = true }, CancellationToken.None);

        var detail = await service.SetBulkAsync(
            series.Id,
            new BulkVolumeStateRequest { ThroughNumber = 3, Owned = true, Format = OwnedFormat.Digital },
            CancellationToken.None);

        detail.ShouldNotBeNull().Volumes.ShouldAllBe(volume => volume.OwnedFormat == OwnedFormat.Digital);
    }

    /// <summary>
    /// Without a format the bulk mark behaves as the single-volume write does: an owned
    /// volume keeps its edition, a newly owned one is physical.
    /// </summary>
    [Fact]
    public async Task SetBulkAsync_OwnedWithoutAFormat_KeepsEditionsAndDefaultsToPhysical()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, volumeCount: 3);
        var service = CreateService(dbContext);

        await service.SetOwnedAsync(
            series.Volumes.Single(volume => volume.Number == 1).Id,
            new SetVolumeOwnedRequest { Owned = true, Format = OwnedFormat.Digital },
            CancellationToken.None);

        var detail = await service.SetBulkAsync(
            series.Id, new BulkVolumeStateRequest { ThroughNumber = 3, Owned = true }, CancellationToken.None);

        detail.ShouldNotBeNull();
        detail.Volumes.Single(volume => volume.Number == 1).OwnedFormat.ShouldBe(OwnedFormat.Digital);
        detail.Volumes.Where(volume => volume.Number > 1)
            .ShouldAllBe(volume => volume.OwnedFormat == OwnedFormat.Physical);
    }

    /// <summary>A format only means anything on volumes being marked owned.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    public async Task SetBulkAsync_AFormatWithoutOwning_IsIgnored(bool? owned)
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, volumeCount: 2);
        var service = CreateService(dbContext);

        await service.SetBulkAsync(
            series.Id, new BulkVolumeStateRequest { ThroughNumber = 2, Owned = true }, CancellationToken.None);

        var detail = await service.SetBulkAsync(
            series.Id,
            new BulkVolumeStateRequest
            {
                ThroughNumber = 2,
                Read = ReadStatus.Read,
                Owned = owned,
                Format = OwnedFormat.Digital,
            },
            CancellationToken.None);

        detail.ShouldNotBeNull();

        // Un-owning clears the edition; leaving ownership alone leaves the physical one.
        OwnedFormat? expected = owned is false ? null : OwnedFormat.Physical;
        detail.Volumes.ShouldAllBe(volume => volume.OwnedFormat == expected);
    }

    [Fact]
    public async Task SetBulkAsync_AnUnknownFormat_IsRefused()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, volumeCount: 2);

        var act = async () => await CreateService(dbContext).SetBulkAsync(
            series.Id,
            new BulkVolumeStateRequest { ThroughNumber = 2, Owned = true, Format = (OwnedFormat)99 },
            CancellationToken.None);

        await act.ShouldThrowAsync<InvalidInputException>();
    }

    [Fact]
    public async Task SetReadAsync_ASeriesWithNoUserStateRow_BecomesReading()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, volumeCount: 2, withSeriesState: false);

        var detail = await CreateService(dbContext).SetReadAsync(
            series.Volumes.Single(candidate => candidate.Number == 1).Id,
            new SetVolumeReadRequest { Status = ReadStatus.Read },
            CancellationToken.None);

        // A missing row reads as PlanToStart, and the first read volume is what moves it on;
        // the row is not left holding the enum's default of Reading by accident.
        (await dbContext.SeriesUserStates.SingleAsync()).TrackingStatus.ShouldBe(TrackingStatus.Reading);
        detail.ShouldNotBeNull().TrackingStatus.ShouldBe(TrackingStatus.Reading);
    }

    [Fact]
    public async Task SetReadAsync_TheLastOutVolumeOfAFinishedSeries_CompletesTheSeries()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(
            dbContext, volumeCount: 2, publicationStatus: PublicationStatus.Completed);
        var service = CreateService(dbContext);

        var first = await service.SetReadAsync(
            series.Volumes.Single(candidate => candidate.Number == 1).Id,
            new SetVolumeReadRequest { Status = ReadStatus.Read },
            CancellationToken.None);
        first.ShouldNotBeNull().TrackingStatus.ShouldBe(TrackingStatus.Reading);

        var last = await service.SetReadAsync(
            series.Volumes.Single(candidate => candidate.Number == 2).Id,
            new SetVolumeReadRequest { Status = ReadStatus.Read },
            CancellationToken.None);

        // The response carries the status, which is how the client picks it up without a reload.
        last.ShouldNotBeNull().TrackingStatus.ShouldBe(TrackingStatus.Completed);
        (await dbContext.SeriesUserStates.SingleAsync()).TrackingStatus.ShouldBe(TrackingStatus.Completed);
    }

    [Fact]
    public async Task SetReadAsync_EverythingReadOnAnOngoingSeries_StaysReading()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, volumeCount: 1);

        await CreateService(dbContext).SetReadAsync(
            series.Volumes.Single().Id,
            new SetVolumeReadRequest { Status = ReadStatus.Read },
            CancellationToken.None);

        (await dbContext.SeriesUserStates.SingleAsync()).TrackingStatus.ShouldBe(TrackingStatus.Reading);
    }

    [Fact]
    public async Task SetReadAsync_UnreadingAVolumeOfACompletedSeries_LeavesItCompleted()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(
            dbContext,
            volumeCount: 2,
            publicationStatus: PublicationStatus.Completed,
            trackingStatus: TrackingStatus.Completed);
        var service = CreateService(dbContext);
        var volumeId = series.Volumes.Single(candidate => candidate.Number == 2).Id;

        await service.SetReadAsync(
            volumeId, new SetVolumeReadRequest { Status = ReadStatus.Read }, CancellationToken.None);
        await service.SetReadAsync(
            volumeId, new SetVolumeReadRequest { Status = ReadStatus.Unread }, CancellationToken.None);

        (await dbContext.SeriesUserStates.SingleAsync()).TrackingStatus.ShouldBe(TrackingStatus.Completed);
    }

    [Fact]
    public async Task SetBulkAsync_MarkingEveryVolumeOfAFinishedSeriesRead_CompletesTheSeries()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(
            dbContext, volumeCount: 3, publicationStatus: PublicationStatus.Completed);

        var detail = await CreateService(dbContext).SetBulkAsync(
            series.Id,
            new BulkVolumeStateRequest { ThroughNumber = 3, Read = ReadStatus.Read },
            CancellationToken.None);

        detail.ShouldNotBeNull().TrackingStatus.ShouldBe(TrackingStatus.Completed);
        (await dbContext.SeriesUserStates.SingleAsync()).TrackingStatus.ShouldBe(TrackingStatus.Completed);
    }

    [Fact]
    public async Task SetBulkAsync_MarkingOnlyOwnership_DoesNotMoveTheTrackingStatus()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(
            dbContext,
            volumeCount: 2,
            publicationStatus: PublicationStatus.Completed,
            trackingStatus: TrackingStatus.PlanToStart);

        await CreateService(dbContext).SetBulkAsync(
            series.Id,
            new BulkVolumeStateRequest { ThroughNumber = 2, Owned = true },
            CancellationToken.None);

        (await dbContext.SeriesUserStates.SingleAsync()).TrackingStatus.ShouldBe(TrackingStatus.PlanToStart);
    }

    [Fact]
    public async Task SetBulkAsync_MarkThroughEight_TouchesTheFirstEightOnly()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, volumeCount: 14);

        var detail = await CreateService(dbContext).SetBulkAsync(
            series.Id,
            new BulkVolumeStateRequest { ThroughNumber = 8, Read = ReadStatus.Read, Owned = true },
            CancellationToken.None);

        detail.ShouldNotBeNull();
        detail.Counts.Read.ShouldBe(8);
        detail.Counts.Owned.ShouldBe(8);

        detail.Volumes.Where(volume => volume.Number <= 8)
            .ShouldAllBe(volume => volume.ReadStatus == ReadStatus.Read && volume.Owned);
        detail.Volumes.Where(volume => volume.Number > 8)
            .ShouldAllBe(volume => volume.ReadStatus == ReadStatus.Unread && !volume.Owned);
    }

    [Fact]
    public async Task SetBulkAsync_OnlyOwnership_LeavesReadStatusAlone()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, volumeCount: 5);
        var service = CreateService(dbContext);

        await service.SetReadAsync(
            series.Volumes.Single(volume => volume.Number == 1).Id,
            new SetVolumeReadRequest { Status = ReadStatus.Read },
            CancellationToken.None);

        var detail = await service.SetBulkAsync(
            series.Id,
            new BulkVolumeStateRequest { ThroughNumber = 5, Owned = true },
            CancellationToken.None);

        detail.ShouldNotBeNull();
        detail.Counts.Owned.ShouldBe(5);

        // A null Read means "leave it": the one already-read volume stays read and the
        // rest stay unread.
        detail.Counts.Read.ShouldBe(1);
    }

    [Fact]
    public async Task SetBulkAsync_SettingNeitherAxis_IsRefused()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, volumeCount: 3);

        var act = async () => await CreateService(dbContext).SetBulkAsync(
            series.Id, new BulkVolumeStateRequest { ThroughNumber = 3 }, CancellationToken.None);

        await act.ShouldThrowAsync<InvalidInputException>();
    }

    [Fact]
    public async Task SetBulkAsync_ThroughMoreVolumesThanExist_TouchesOnlyWhatIsThere()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, volumeCount: 3);

        var detail = await CreateService(dbContext).SetBulkAsync(
            series.Id,
            new BulkVolumeStateRequest { ThroughNumber = 99, Read = ReadStatus.Read },
            CancellationToken.None);

        detail.ShouldNotBeNull().Counts.Read.ShouldBe(3);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task SetBulkAsync_ANonPositiveRange_IsRefused(int throughNumber)
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, volumeCount: 3);

        var act = async () => await CreateService(dbContext).SetBulkAsync(
            series.Id,
            new BulkVolumeStateRequest { ThroughNumber = throughNumber, Read = ReadStatus.Read },
            CancellationToken.None);

        // The caller's input, so a 400 — not the 500 an ArgumentOutOfRangeException was.
        await act.ShouldThrowAsync<InvalidInputException>();
    }

    [Fact]
    public async Task SetReadAsync_UnknownVolume_ReturnsNull()
    {
        await using var dbContext = CreateDbContext();

        (await CreateService(dbContext).SetReadAsync(
            Guid.CreateVersion7(), new SetVolumeReadRequest { Status = ReadStatus.Read }, CancellationToken.None))
            .ShouldBeNull();
    }

    [Fact]
    public async Task SetBulkAsync_UnknownSeries_ReturnsNull()
    {
        await using var dbContext = CreateDbContext();

        (await CreateService(dbContext).SetBulkAsync(
            Guid.CreateVersion7(),
            new BulkVolumeStateRequest { ThroughNumber = 1, Read = ReadStatus.Read },
            CancellationToken.None))
            .ShouldBeNull();
    }

    /// <summary>
    /// The remaining single-volume write on an id that is not there. Null is what
    /// the controller turns into the 404.
    /// </summary>
    [Fact]
    public async Task SetOwnedAsync_UnknownVolume_ReturnsNull()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var missing = Guid.CreateVersion7();

        (await service.SetOwnedAsync(
            missing, new SetVolumeOwnedRequest { Owned = true }, CancellationToken.None)).ShouldBeNull();
    }

    /// <summary>
    /// The volume a write was about, read back out of the series it answers with.
    /// </summary>
    private static VolumeResponse Written(SeriesDetailResponse? detail, Guid volumeId) =>
        detail.ShouldNotBeNull().Volumes.Single(volume => volume.VolumeId == volumeId);

    private static VolumeCommandService CreateService(LnShelfDbContext dbContext) =>
        new(dbContext, TimeProvider.System, NullLogger<VolumeCommandService>.Instance);

    /// <summary>
    /// The series' "last read" date follows the volumes being read. Without this the
    /// library's Last-read sort and the hero line stay frozen at import time forever.
    /// </summary>
    [Fact]
    public async Task SetReadAsync_AVolumeBecomingRead_MovesTheSeriesLastReadDateToToday()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, volumeCount: 3);

        await CreateService(dbContext).SetReadAsync(
            series.Volumes.Single(candidate => candidate.Number == 2).Id,
            new SetVolumeReadRequest { Status = ReadStatus.Read },
            CancellationToken.None);

        (await dbContext.SeriesUserStates.SingleAsync()).LastReadAt
            .ShouldBe(DateOnly.FromDateTime(DateTime.Now.Date));
    }

    /// <summary>
    /// Forward only: "last read" is the most recent reading, not the most recent edit.
    /// </summary>
    // The case this guards is ordinary — ticking off an old volume you finished months ago
    // — and getting it wrong would silently rewrite the shelf's reading history.
    [Fact]
    public async Task SetReadAsync_ASeriesReadMoreRecentlyThanToday_KeepsTheLaterDate()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, volumeCount: 2);

        var later = DateOnly.FromDateTime(DateTime.Now.Date).AddDays(3);
        series.UserState.ShouldNotBeNull().LastReadAt = later;
        await dbContext.SaveChangesAsync();

        await CreateService(dbContext).SetReadAsync(
            series.Volumes.Single(candidate => candidate.Number == 1).Id,
            new SetVolumeReadRequest { Status = ReadStatus.Read },
            CancellationToken.None);

        (await dbContext.SeriesUserStates.SingleAsync()).LastReadAt.ShouldBe(later);
    }

    [Fact]
    public async Task SetReadAsync_AVolumeGoingBackToUnread_LeavesTheSeriesDateAlone()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, volumeCount: 1);

        var stamped = new DateOnly(2021, 11, 23);
        series.UserState.ShouldNotBeNull().LastReadAt = stamped;
        await dbContext.SaveChangesAsync();

        await CreateService(dbContext).SetReadAsync(
            series.Volumes.Single().Id,
            new SetVolumeReadRequest { Status = ReadStatus.Unread },
            CancellationToken.None);

        // Un-ticking one volume does not mean the series was never read; the date belongs
        // to the reading, not to the row's last edit.
        (await dbContext.SeriesUserStates.SingleAsync()).LastReadAt.ShouldBe(stamped);
    }

    [Fact]
    public async Task SetBulkAsync_MarkingVolumesRead_MovesTheSeriesLastReadDateToToday()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, volumeCount: 8);

        await CreateService(dbContext).SetBulkAsync(
            series.Id,
            new BulkVolumeStateRequest { ThroughNumber = 5, Read = ReadStatus.Read },
            CancellationToken.None);

        (await dbContext.SeriesUserStates.SingleAsync()).LastReadAt
            .ShouldBe(DateOnly.FromDateTime(DateTime.Now.Date));
    }

    [Fact]
    public async Task SetBulkAsync_MarkingVolumesOwnedOnly_LeavesTheSeriesDateAlone()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, volumeCount: 4);

        await CreateService(dbContext).SetBulkAsync(
            series.Id,
            new BulkVolumeStateRequest { ThroughNumber = 4, Owned = true },
            CancellationToken.None);

        // Buying is not reading. An import that marks a shelf owned must not claim the
        // whole thing was read today.
        (await dbContext.SeriesUserStates.SingleAsync()).LastReadAt.ShouldBeNull();
    }

    private static async Task<Series> SeedAsync(
        LnShelfDbContext dbContext,
        int volumeCount,
        bool withSeriesState = true,
        PublicationStatus publicationStatus = PublicationStatus.Ongoing,
        TrackingStatus trackingStatus = TrackingStatus.Reading)
    {
        var series = new Series
        {
            Id = Guid.CreateVersion7(),
            AniListId = 94846,
            TitleEnglish = "The Saga of Tanya the Evil",
            Format = Format.LightNovel,
            PublicationStatus = publicationStatus,
            UserState = withSeriesState ? new SeriesUserState { TrackingStatus = trackingStatus } : null,
        };

        foreach (var number in Enumerable.Range(1, volumeCount))
        {
            series.Volumes.Add(new Volume
            {
                Id = Guid.CreateVersion7(),
                Number = number,

                // In print with no date — the real shape of this shelf.
                IsReleased = true,
            });
        }

        dbContext.Series.Add(series);
        await dbContext.SaveChangesAsync();

        return series;
    }

    private static LnShelfDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<LnShelfDbContext>()
            .UseInMemoryDatabase($"volumes-{Guid.CreateVersion7()}")
            .Options, TestUsers.Current);
}
