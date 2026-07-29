using LnShelf.Api.Data;
using LnShelf.Api.Errors;
using LnShelf.Api.Features.Covers.Models;
using LnShelf.Api.Features.Covers.Services;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace LnShelf.Api.Tests.Features.Shelf.Services;

/// <summary>
/// The per-volume writes behind the tile: the read cycle, the owned toggle, and the
/// bulk "mark 1–N" an imported collection needs.
/// </summary>
public sealed class VolumeCommandServiceTests
{
    [Fact]
    public async Task SetReadAsync_AVolumeWithNoStateRow_CreatesOneAndStampsTheDate()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, volumeCount: 3);
        var volume = series.Volumes.Single(candidate => candidate.Number == 1);

        var response = await CreateService(dbContext).SetReadAsync(
            volume.Id, new SetVolumeReadRequest { Status = ReadStatus.Read }, CancellationToken.None);

        response.ShouldNotBeNull();
        response.ReadStatus.ShouldBe(ReadStatus.Read);
        response.ReadAt.ShouldNotBeNull();

        // Absence of a row means Unread and NotOwned; touching it is what creates one.
        (await dbContext.VolumeUserStates.CountAsync()).ShouldBe(1);
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

        var response = await service.SetReadAsync(
            volume.Id, new SetVolumeReadRequest { Status = ReadStatus.Unread }, CancellationToken.None);

        // A read date on an unread volume is a lie the stats page would later believe.
        response.ShouldNotBeNull().ReadAt.ShouldBeNull();
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

        var response = await service.SetReadAsync(
            volume.Id, new SetVolumeReadRequest { Status = ReadStatus.Reading }, CancellationToken.None);

        // The tile needs this: a reading tile has to be one of the four in-print states,
        // never a hatched "not out" one.
        response.ShouldNotBeNull().IsOut.ShouldBeTrue();
    }

    [Fact]
    public async Task SetOwnedAsync_WithNoFormatGiven_DefaultsToPhysical()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, volumeCount: 1);

        var response = await CreateService(dbContext).SetOwnedAsync(
            series.Volumes.Single().Id, new SetVolumeOwnedRequest { Owned = true }, CancellationToken.None);

        response.ShouldNotBeNull();
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

        var response = await service.SetOwnedAsync(
            volumeId, new SetVolumeOwnedRequest { Owned = false }, CancellationToken.None);

        response.ShouldNotBeNull();
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
    public async Task SetBulkAsync_ANonPositiveRange_Throws(int throughNumber)
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, volumeCount: 3);

        var act = async () => await CreateService(dbContext).SetBulkAsync(
            series.Id,
            new BulkVolumeStateRequest { ThroughNumber = throughNumber, Read = ReadStatus.Read },
            CancellationToken.None);

        await act.ShouldThrowAsync<ArgumentOutOfRangeException>();
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

    private static VolumeCommandService CreateService(
        LnShelfDbContext dbContext,
        ICoverStore? coverStore = null) =>
        new(dbContext,
            coverStore ?? Substitute.For<ICoverStore>(),
            new CoverNormalizer(
                Options.Create(new CoverStoreOptions()),
                NullLogger<CoverNormalizer>.Instance),
            TimeProvider.System,
            NullLogger<VolumeCommandService>.Instance);

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
        var response = await CreateService(dbContext, coverStore).SetCoverAsync(
            volume.Id, CreatePng(600, 900), CancellationToken.None);

        response.ShouldNotBeNull();
        response.HasCoverOverride.ShouldBeTrue();
        response.CoverObjectKey.ShouldBe($"override/volume/{volume.Id}.webp");

        await coverStore.Received(1).SaveAsync(
            $"override/volume/{volume.Id}.webp",
            Arg.Any<ReadOnlyMemory<byte>>(),
            "image/webp",
            Arg.Any<CancellationToken>());
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
            series.Volumes.Single().Id, CreatePng(2400, 3600), CancellationToken.None);

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
        await service.SetCoverAsync(volume.Id, CreatePng(400, 600), CancellationToken.None);

        var response = await service.RemoveCoverAsync(volume.Id, CancellationToken.None);

        response.ShouldNotBeNull();
        response.HasCoverOverride.ShouldBeFalse();
        response.CoverObjectKey.ShouldBe("anilist/94846.jpg");
    }

    /// <summary>A real PNG, because the normalizer decodes what it is given.</summary>
    private static byte[] CreatePng(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height);
        using var stream = new MemoryStream();
        image.Save(stream, new SixLabors.ImageSharp.Formats.Png.PngEncoder());
        return stream.ToArray();
    }

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
            .Options);
}
