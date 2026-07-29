using LnShelf.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace LnShelf.Api.Tests.Features.Shelf.Services;

/// <summary>
/// Add-series idempotency (testing.md #3 — the bugs that corrupt data quietly).
/// Adding the same series twice must return the first one and write nothing.
/// </summary>
// The store is the in-memory provider and the metadata source is a substitute, so
// neither Postgres nor AniList is touched. The unique index on AniListId is the
// database's backstop; what is asserted here is the behaviour that keeps the
// backstop from ever firing.
public sealed class SeriesCommandServiceTests
{
    /// <summary>AniList's media id for the Mushoku Tensei light novel.</summary>
    private const int AniListId = 85470;

    private static readonly DateOnly Today = new(2026, 7, 29);

    [Fact]
    public async Task AddFromAniListAsync_NewSeries_PersistsTheSeriesItsVolumesAndDefaultUserState()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, CreateMetadataSource());

        var response = await service.AddFromAniListAsync(CreateRequest(), CancellationToken.None);

        response.ShouldNotBeNull();
        response.AlreadyTracked.ShouldBeFalse();
        response.TitleEnglish.ShouldBe("Mushoku Tensei: Jobless Reincarnation");
        response.Format.ShouldBe(Format.LightNovel);
        response.VolumeCount.ShouldBe(26);

        var series = await dbContext.Series
            .Include(candidate => candidate.Volumes)
            .Include(candidate => candidate.UserState)
            .SingleAsync(candidate => candidate.Id == response.SeriesId);

        series.AniListId.ShouldBe(AniListId);
        series.MangaUpdatesId.ShouldBeNull();
        series.PublicationStatus.ShouldBe(PublicationStatus.Completed);
        series.Author.ShouldBe("Rifujin na Magonote");
        series.LastSyncedAt.ShouldNotBeNull();

        series.Volumes.Count.ShouldBe(26);
        series.Volumes.Select(volume => volume.Number).Order().ShouldBe(Enumerable.Range(1, 26));
        series.Volumes.ShouldAllBe(volume => !volume.IsLocalOnly);

        // A just-added series is on the shelf and unread: nothing else would be
        // honest, and it matches what the projection assumes for a missing row.
        series.UserState.ShouldNotBeNull().TrackingStatus.ShouldBe(TrackingStatus.PlanToStart);
        series.UserState.HasNewVolume.ShouldBeFalse();
    }

    [Fact]
    public async Task AddFromAniListAsync_TheSameSeriesTwice_ReturnsTheExistingSeriesAndWritesNothing()
    {
        await using var dbContext = CreateDbContext();
        var metadataSource = CreateMetadataSource();
        var service = CreateService(dbContext, metadataSource);

        var first = await service.AddFromAniListAsync(CreateRequest(), CancellationToken.None);
        var second = await service.AddFromAniListAsync(CreateRequest(), CancellationToken.None);

        first.ShouldNotBeNull();
        second.ShouldNotBeNull();

        first.AlreadyTracked.ShouldBeFalse();
        second.AlreadyTracked.ShouldBeTrue();
        second.SeriesId.ShouldBe(first.SeriesId);
        second.VolumeCount.ShouldBe(first.VolumeCount);

        // One row, one set of volumes, no second user-state row.
        (await dbContext.Series.CountAsync()).ShouldBe(1);
        (await dbContext.Volumes.CountAsync()).ShouldBe(26);
        (await dbContext.SeriesUserStates.CountAsync()).ShouldBe(1);

        // And AniList was spared the second request entirely.
        await metadataSource.Received(1).GetSeriesAsync(AniListId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddFromAniListAsync_UnknownAniListId_ReturnsNullAndAddsNothing()
    {
        await using var dbContext = CreateDbContext();
        var metadataSource = Substitute.For<ISeriesMetadataSource>();
        metadataSource.GetSeriesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((SeriesMetadata?)null);

        var response = await CreateService(dbContext, metadataSource)
            .AddFromAniListAsync(CreateRequest(), CancellationToken.None);

        response.ShouldBeNull();
        (await dbContext.Series.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task AddFromAniListAsync_RequestedFormatDisagreesWithTheSource_StoresTheSourcesFormat()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, CreateMetadataSource());

        // The id is the light novel's; the caller claims manga.
        var response = await service.AddFromAniListAsync(
            new AddSeriesRequest { AniListId = AniListId, Format = Format.Manga },
            CancellationToken.None);

        response.ShouldNotBeNull().Format.ShouldBe(Format.LightNovel);
        (await dbContext.Series.SingleAsync()).Format.ShouldBe(Format.LightNovel);
    }

    [Fact]
    public async Task AddFromAniListAsync_NewSeries_IsNotBehindBecauseNoReleaseDatesAreKnownYet()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, CreateMetadataSource());

        await service.AddFromAniListAsync(CreateRequest(), CancellationToken.None);

        var series = await dbContext.Series
            .Include(candidate => candidate.Volumes)
            .Include(candidate => candidate.UserState)
            .SingleAsync();

        var card = LibraryCardProjection.ToCard(Today).Compile()(series);

        // 26 volumes tracked, none of them dated, so none of them counts as
        // published — and nothing released means nothing to buy. The series
        // therefore lands on the "All" and "Plan to start" shelves, not "Behind".
        // MangaUpdates supplying dates in phase 2 is what moves it.
        card.Tracked.ShouldBe(26);
        card.Published.ShouldBe(0);
        card.ToBuy.ShouldBe(0);
        card.Backlog.ShouldBe(0);
        card.Read.ShouldBe(0);
        card.Owned.ShouldBe(0);
        card.NextToBuyNumber.ShouldBeNull();
        card.Behind.ShouldBeFalse();
        card.Matches(LibraryFilter.UpToDate).ShouldBeTrue();
        card.Matches(LibraryFilter.PlanToStart).ShouldBeTrue();
    }

    private static SeriesCommandService CreateService(
        LnShelfDbContext dbContext,
        ISeriesMetadataSource metadataSource) =>
        new(dbContext, metadataSource, TimeProvider.System, NullLogger<SeriesCommandService>.Instance);

    private static ISeriesMetadataSource CreateMetadataSource()
    {
        var metadataSource = Substitute.For<ISeriesMetadataSource>();
        metadataSource.GetSeriesAsync(AniListId, Arg.Any<CancellationToken>())
            .Returns(CreateMetadata());

        return metadataSource;
    }

    /// <summary>
    /// The same shape AniListMediaMapper produces from the captured fixture —
    /// 26 volumes, none of them dated.
    /// </summary>
    private static SeriesMetadata CreateMetadata() => new()
    {
        ExternalId = AniListId,
        TitleEnglish = "Mushoku Tensei: Jobless Reincarnation",
        TitleRomaji = "Mushoku Tensei: Isekai Ittara Honki Dasu",
        TitleNative = "無職転生 ～異世界行ったら本気だす～",
        Format = Format.LightNovel,
        PublicationStatus = PublicationStatus.Completed,
        StartYear = 2014,
        Genres = ["Adventure", "Drama", "Ecchi", "Fantasy"],
        Author = "Rifujin na Magonote",
        Illustrator = "Sirotaka",
        Volumes = [.. Enumerable.Range(1, 26).Select(number => new VolumeMetadata { Number = number })],
    };

    private static AddSeriesRequest CreateRequest() =>
        new() { AniListId = AniListId, Format = Format.LightNovel };

    private static LnShelfDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<LnShelfDbContext>()
            .UseInMemoryDatabase($"shelf-{Guid.CreateVersion7()}")
            .Options);
}
