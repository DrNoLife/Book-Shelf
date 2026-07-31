using LnShelf.Api.Data;
using LnShelf.Shared.Preferences;
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

    private const string CoverObjectKey = "anilist/85470.jpg";

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

        // The cover is fetched when the series is added, not on first render: the
        // shelf has to work with the AniList CDN down.
        series.CoverObjectKey.ShouldBe(CoverObjectKey);

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

    /// <summary>
    /// The per-series volume view. Null is a value, not an omission: it is how a series
    /// goes back to following the global preference.
    /// </summary>
    [Theory]
    [InlineData(VolumeView.List)]
    [InlineData(VolumeView.Tiles)]
    public async Task SetVolumeViewAsync_AChosenView_IsStoredOnTheSeriesAndComesBackOnTheDetail(
        VolumeView view)
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, CreateMetadataSource());
        var added = await service.AddFromAniListAsync(CreateRequest(), CancellationToken.None);

        var detail = await service.SetVolumeViewAsync(
            added.ShouldNotBeNull().SeriesId,
            new SetSeriesVolumeViewRequest { View = view },
            CancellationToken.None);

        detail.ShouldNotBeNull().VolumeViewOverride.ShouldBe(view);
        (await dbContext.SeriesUserStates.SingleAsync()).VolumeViewOverride.ShouldBe(view);
    }

    [Fact]
    public async Task SetVolumeViewAsync_ANullView_ClearsTheOverrideSoTheSeriesFollowsTheGlobal()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, CreateMetadataSource());
        var added = await service.AddFromAniListAsync(CreateRequest(), CancellationToken.None);
        var seriesId = added.ShouldNotBeNull().SeriesId;

        await service.SetVolumeViewAsync(
            seriesId, new SetSeriesVolumeViewRequest { View = VolumeView.List }, CancellationToken.None);

        var detail = await service.SetVolumeViewAsync(
            seriesId, new SetSeriesVolumeViewRequest { View = null }, CancellationToken.None);

        detail.ShouldNotBeNull().VolumeViewOverride.ShouldBeNull();
        (await dbContext.SeriesUserStates.SingleAsync()).VolumeViewOverride.ShouldBeNull();

        // One user-state row throughout: choosing a view is a write to the row the
        // series already had, never a second one.
        (await dbContext.SeriesUserStates.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task SetVolumeViewAsync_UnknownSeries_ReturnsNullAndWritesNothing()
    {
        await using var dbContext = CreateDbContext();

        var detail = await CreateService(dbContext, CreateMetadataSource()).SetVolumeViewAsync(
            Guid.CreateVersion7(),
            new SetSeriesVolumeViewRequest { View = VolumeView.List },
            CancellationToken.None);

        detail.ShouldBeNull();
        (await dbContext.SeriesUserStates.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task AddFromAniListAsync_ANewSeries_AdoptsItsStagedIsbnsThenRefreshesIt()
    {
        await using var dbContext = CreateDbContext();

        var isbnBackfill = Substitute.For<IIsbnBackfill>();
        isbnBackfill.RunForSeriesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new IsbnBackfillReport
            {
                Offered = 14,
                Written = 14,
                AlreadyRecorded = 0,
                WithoutVolume = 0,
            });

        var sync = Substitute.For<ISeriesSyncService>();
        sync.RunForSeriesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new SyncRun
            {
                Id = Guid.CreateVersion7(),
                StartedAt = DateTimeOffset.UnixEpoch,
                Summary = "Mushoku Tensei refreshed · 14 volumes enriched from OpenLibrary",
            });

        var response = await CreateService(dbContext, CreateMetadataSource(), isbnBackfill, sync)
            .AddFromAniListAsync(CreateRequest(), CancellationToken.None);

        // The order is the whole point: enrichment is keyed on ISBN, so the ISBNs have to be
        // on the volumes before the refresh looks at them. Adopting after would enrich
        // nothing and the caller would never know why.
        Received.InOrder(() =>
        {
            isbnBackfill.RunForSeriesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
            sync.RunForSeriesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        });

        // And what the refresh managed comes back, so the page that asked can say so.
        response.ShouldNotBeNull().RefreshSummary
            .ShouldNotBeNull().ShouldContain("14 volumes enriched");
    }

    [Fact]
    public async Task AddFromAniListAsync_ASeriesAlreadyTracked_DoesNotRefreshItAgain()
    {
        await using var dbContext = CreateDbContext();

        var isbnBackfill = NoStagedIsbns();
        var sync = NoRefresh();
        var service = CreateService(dbContext, CreateMetadataSource(), isbnBackfill, sync);

        await service.AddFromAniListAsync(CreateRequest(), CancellationToken.None);
        var second = await service.AddFromAniListAsync(CreateRequest(), CancellationToken.None);

        second.ShouldNotBeNull().AlreadyTracked.ShouldBeTrue();

        // Adding twice is a no-op, and that has to include the refresh: pressing Add on a
        // series already on the shelf must not spend anyone's rate limit.
        await sync.Received(1).RunForSeriesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await isbnBackfill.Received(1).RunForSeriesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddFromAniListAsync_ARefreshThatThrows_StillAddsTheSeries()
    {
        await using var dbContext = CreateDbContext();

        var sync = Substitute.For<ISeriesSyncService>();
        sync.RunForSeriesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns<Task<SyncRun?>>(_ => throw new HttpRequestException("MangaUpdates is down"));

        var response = await CreateService(dbContext, CreateMetadataSource(), NoStagedIsbns(), sync)
            .AddFromAniListAsync(CreateRequest(), CancellationToken.None);

        // The series is on the shelf by the time the refresh runs, so failing the add would
        // be the wrong trade — the refresh is an improvement on the add, not part of it.
        response.ShouldNotBeNull().AlreadyTracked.ShouldBeFalse();
        response.VolumeCount.ShouldBe(26);

        // Said with silence rather than a lie: no summary, because nothing was refreshed.
        response.RefreshSummary.ShouldBeNull();

        (await dbContext.Series.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task DeleteAsync_ATrackedSeries_TakesItsVolumesAndAllUserStateWithIt()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, CreateMetadataSource());

        var added = await service.AddFromAniListAsync(CreateRequest(), CancellationToken.None);
        var seriesId = added.ShouldNotBeNull().SeriesId;

        // Give it the state a real series accumulates, so the delete has something to lose.
        var volume = await dbContext.Volumes.FirstAsync(candidate => candidate.Number == 1);
        volume.UserState = new VolumeUserState
        {
            VolumeId = volume.Id,
            ReadStatus = ReadStatus.Read,
            Owned = true,
            PurchasePrice = 180,
        };
        // Adding a series already creates its user-state row, so this promotes the one
        // that is there rather than making a second.
        var seriesState = await dbContext.SeriesUserStates.SingleAsync();
        seriesState.TrackingStatus = TrackingStatus.Reading;
        await dbContext.SaveChangesAsync();

        (await service.DeleteAsync(seriesId, CancellationToken.None)).ShouldBeTrue();

        // All four tables, by the cascades in SeriesConfiguration and VolumeConfiguration.
        // A series row left behind with no volumes, or volumes with no series, would both
        // be worse than either outcome.
        (await dbContext.Series.CountAsync()).ShouldBe(0);
        (await dbContext.Volumes.CountAsync()).ShouldBe(0);
        (await dbContext.VolumeUserStates.CountAsync()).ShouldBe(0);
        (await dbContext.SeriesUserStates.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task DeleteAsync_UnknownSeries_IsFalseRatherThanAFailure()
    {
        await using var dbContext = CreateDbContext();

        // Nothing to delete is an answer, not an error — a stale page sending the request
        // twice should get a plain "not here" the second time.
        (await CreateService(dbContext, CreateMetadataSource())
            .DeleteAsync(Guid.CreateVersion7(), CancellationToken.None)).ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteAsync_ASeriesAnotherOneIsLinkedTo_ClearsTheDanglingRelation()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, CreateMetadataSource());

        var added = await service.AddFromAniListAsync(CreateRequest(), CancellationToken.None);
        var deletedId = added.ShouldNotBeNull().SeriesId;

        // The manga adaptation of the light novel being removed. RelatedSeriesId is a plain
        // column with no foreign key, so nothing in the database would stop it pointing at
        // a row that is gone.
        var manga = new Series
        {
            Id = Guid.CreateVersion7(),
            AniListId = 999,
            TitleEnglish = "Mushoku Tensei (manga)",
            Format = Format.Manga,
            PublicationStatus = PublicationStatus.Ongoing,
            RelatedSeriesId = deletedId,
        };
        dbContext.Series.Add(manga);
        await dbContext.SaveChangesAsync();

        await service.DeleteAsync(deletedId, CancellationToken.None);

        var survivor = await dbContext.Series.SingleAsync();
        survivor.TitleEnglish.ShouldBe("Mushoku Tensei (manga)");

        // "Tracked separately" is what the relation panel says; a link to a series that no
        // longer exists is not that.
        survivor.RelatedSeriesId.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteAsync_OneOfTwoSeries_LeavesTheOtherEntirelyAlone()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, CreateMetadataSource());

        var added = await service.AddFromAniListAsync(CreateRequest(), CancellationToken.None);

        var keeper = new Series
        {
            Id = Guid.CreateVersion7(),
            AniListId = 999,
            TitleEnglish = "Overlord",
            Format = Format.LightNovel,
            PublicationStatus = PublicationStatus.Ongoing,
        };
        keeper.Volumes.Add(new Volume
        {
            Id = Guid.CreateVersion7(),
            Number = 1,
            UserState = new VolumeUserState { ReadStatus = ReadStatus.Read, Owned = true },
        });
        dbContext.Series.Add(keeper);
        await dbContext.SaveChangesAsync();

        await service.DeleteAsync(added.ShouldNotBeNull().SeriesId, CancellationToken.None);

        (await dbContext.Series.SingleAsync()).TitleEnglish.ShouldBe("Overlord");
        (await dbContext.Volumes.SingleAsync()).Number.ShouldBe(1);
        (await dbContext.VolumeUserStates.CountAsync()).ShouldBe(1);
    }

    private static SeriesCommandService CreateService(
        LnShelfDbContext dbContext,
        ISeriesMetadataSource metadataSource,
        IIsbnBackfill? isbnBackfill = null,
        ISeriesSyncService? seriesSync = null) =>
        new(dbContext,
            metadataSource,
            CreateCoverCache(),
            isbnBackfill ?? NoStagedIsbns(),
            seriesSync ?? NoRefresh(),
            TimeProvider.System,
            NullLogger<SeriesCommandService>.Instance);

    /// <summary>Staging holds nothing for this series — the usual case for a new one.</summary>
    private static IIsbnBackfill NoStagedIsbns()
    {
        var backfill = Substitute.For<IIsbnBackfill>();
        backfill.RunForSeriesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new IsbnBackfillReport
            {
                Offered = 0,
                Written = 0,
                AlreadyRecorded = 0,
                WithoutVolume = 0,
            });

        return backfill;
    }

    /// <summary>
    /// A refresh that answers nothing, so the add tests stay about adding.
    /// </summary>
    // Null is a real answer here, not a fake one: it is what a refresh returns when the
    // series has no external sources to reach, and the add must not depend on more.
    private static ISeriesSyncService NoRefresh()
    {
        var sync = Substitute.For<ISeriesSyncService>();
        sync.RunForSeriesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((SyncRun?)null);

        return sync;
    }

    /// <summary>Pretends the cover was cached, so the stored key can be asserted.</summary>
    private static ICoverCache CreateCoverCache()
    {
        var coverCache = Substitute.For<ICoverCache>();
        coverCache.EnsureCachedAsync(AniListId, Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(CoverObjectKey);

        return coverCache;
    }

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
