using LnShelf.Shared.Preferences;
using Microsoft.EntityFrameworkCore;

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

    /// <summary>MangaUpdates' series id for Fremd Torturchen — eleven digits, hence a long.</summary>
    private const long MangaUpdatesId = 17992364412;

    private const string MangaUpdatesCoverObjectKey = "mangaupdates/17992364412.jpg";

    private static readonly DateOnly Today = new(2026, 7, 29);

    [Fact]
    public async Task AddAsync_AniList_NewSeries_PersistsTheSeriesItsVolumesAndDefaultUserState()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, CreateMetadataSource());

        var response = await service.AddAsync(CreateRequest(), CancellationToken.None);

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
        // Only a refresh stamps it; the add-time refresh is stubbed out here.
        series.LastSyncedAt.ShouldBeNull();

        // The cover is fetched when the series is added, not on first render: the
        // shelf has to work with the AniList CDN down.
        series.CoverObjectKey.ShouldBe(CoverObjectKey);

        series.Volumes.Count.ShouldBe(26);
        series.Volumes.Select(volume => (int)volume.Number).Order().ShouldBe(Enumerable.Range(1, 26));
        series.Volumes.ShouldAllBe(volume => !volume.IsLocalOnly);

        // A just-added series is on the shelf and unread: nothing else would be
        // honest, and it matches what the projection assumes for a missing row.
        series.UserState.ShouldNotBeNull().TrackingStatus.ShouldBe(TrackingStatus.PlanToStart);
        series.UserState.HasNewVolume.ShouldBeFalse();
    }

    [Fact]
    public async Task AddAsync_AniList_TheReleaseSourceCountsFewerVolumes_CreatesOnlyItsCount()
    {
        // Torture Princess: AniList counts ten (nine plus the bonus Vol. 7.5), the release
        // source's English edition nine. Created from AniList's ten, volume 10 was a phantom
        // that sync could never remove.
        await using var dbContext = CreateDbContext();
        var matcher = Substitute.For<IReleaseSourceMatcher>();
        matcher.MatchAsync(Arg.Any<Series>(), Arg.Any<IReadOnlySet<long>>(), Arg.Any<CancellationToken>())
            .Returns(new ReleaseSourceMatch
            {
                Outcome = ReleaseMatchOutcome.Linked,
                MatchedExternalId = 17992364412,
                MatchedSeries = new ReleaseSeriesInfo { ExternalId = 17992364412, Title = "Fremd Torturchen", VolumeCount = 9 },
                Explanation = "Linked in this test.",
            });
        var service = CreateService(dbContext, CreateMetadataSource(), releaseSourceMatcher: matcher);

        var response = await service.AddAsync(CreateRequest(), CancellationToken.None);

        var series = await dbContext.Series
            .Include(candidate => candidate.Volumes)
            .SingleAsync(candidate => candidate.Id == response!.SeriesId);

        series.MangaUpdatesId.ShouldBe(17992364412);
        series.Volumes.Select(volume => (int)volume.Number).Order().ShouldBe(Enumerable.Range(1, 9));
    }

    [Fact]
    public async Task AddAsync_AniList_TheReleaseSourceIsDown_KeepsAniListsVolumes()
    {
        await using var dbContext = CreateDbContext();
        var matcher = Substitute.For<IReleaseSourceMatcher>();
        matcher.MatchAsync(Arg.Any<Series>(), Arg.Any<IReadOnlySet<long>>(), Arg.Any<CancellationToken>())
            .Returns<Task<ReleaseSourceMatch>>(_ => throw new MetadataSourceUnavailableException("MangaUpdates", "down in this test", 503));
        var service = CreateService(dbContext, CreateMetadataSource(), releaseSourceMatcher: matcher);

        var response = await service.AddAsync(CreateRequest(), CancellationToken.None);

        response.ShouldNotBeNull().VolumeCount.ShouldBe(26);
        (await dbContext.Series.SingleAsync()).MangaUpdatesId.ShouldBeNull();
    }

    [Fact]
    public async Task AddAsync_AniList_TheSameSeriesTwice_ReturnsTheExistingSeriesAndWritesNothing()
    {
        await using var dbContext = CreateDbContext();
        var metadataSource = CreateMetadataSource();
        var service = CreateService(dbContext, metadataSource);

        var first = await service.AddAsync(CreateRequest(), CancellationToken.None);
        var second = await service.AddAsync(CreateRequest(), CancellationToken.None);

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
    public async Task AddAsync_AniList_UnknownAniListId_ReturnsNullAndAddsNothing()
    {
        await using var dbContext = CreateDbContext();
        var metadataSource = Substitute.For<ISeriesMetadataSource>();
        metadataSource.GetSeriesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((SeriesMetadata?)null);

        var response = await CreateService(dbContext, metadataSource)
            .AddAsync(CreateRequest(), CancellationToken.None);

        response.ShouldBeNull();
        (await dbContext.Series.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task AddAsync_AniList_RequestedFormatDisagreesWithTheSource_StoresTheSourcesFormat()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, CreateMetadataSource());

        // The id is the light novel's; the caller claims manga.
        var response = await service.AddAsync(
            new AddSeriesRequest { AniListId = AniListId, Format = Format.Manga },
            CancellationToken.None);

        response.ShouldNotBeNull().Format.ShouldBe(Format.LightNovel);
        (await dbContext.Series.SingleAsync()).Format.ShouldBe(Format.LightNovel);
    }

    [Fact]
    public async Task AddAsync_AniList_NewSeries_IsNotBehindBecauseNoReleaseDatesAreKnownYet()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, CreateMetadataSource());

        await service.AddAsync(CreateRequest(), CancellationToken.None);

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
        var added = await service.AddAsync(CreateRequest(), CancellationToken.None);

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
        var added = await service.AddAsync(CreateRequest(), CancellationToken.None);
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
    public async Task AddAsync_AniList_ANewSeries_AdoptsItsStagedIsbnsThenRefreshesIt()
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
            .AddAsync(CreateRequest(), CancellationToken.None);

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
    public async Task AddAsync_AniList_ASeriesAlreadyTracked_DoesNotRefreshItAgain()
    {
        await using var dbContext = CreateDbContext();

        var isbnBackfill = NoStagedIsbns();
        var sync = NoRefresh();
        var service = CreateService(dbContext, CreateMetadataSource(), isbnBackfill, sync);

        await service.AddAsync(CreateRequest(), CancellationToken.None);
        var second = await service.AddAsync(CreateRequest(), CancellationToken.None);

        second.ShouldNotBeNull().AlreadyTracked.ShouldBeTrue();

        // Adding twice is a no-op, and that has to include the refresh: pressing Add on a
        // series already on the shelf must not spend anyone's rate limit.
        await sync.Received(1).RunForSeriesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await isbnBackfill.Received(1).RunForSeriesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddAsync_AniList_ARefreshThatThrows_StillAddsTheSeries()
    {
        await using var dbContext = CreateDbContext();

        var sync = Substitute.For<ISeriesSyncService>();
        sync.RunForSeriesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns<Task<SyncRun?>>(_ => throw new HttpRequestException("MangaUpdates is down"));

        var response = await CreateService(dbContext, CreateMetadataSource(), NoStagedIsbns(), sync)
            .AddAsync(CreateRequest(), CancellationToken.None);

        // The series is on the shelf by the time the refresh runs, so failing the add would
        // be the wrong trade — the refresh is an improvement on the add, not part of it.
        response.ShouldNotBeNull().AlreadyTracked.ShouldBeFalse();
        response.VolumeCount.ShouldBe(26);

        // Said with silence rather than a lie: no summary, because nothing was refreshed.
        response.RefreshSummary.ShouldBeNull();

        (await dbContext.Series.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task AddAsync_MangaUpdates_NewSeries_PersistsWhatMangaUpdatesKnows()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(
            dbContext, CreateMetadataSource(), mangaUpdatesSource: CreateMangaUpdatesSource());

        var response = await service.AddAsync(
            CreateMangaUpdatesRequest(), CancellationToken.None);

        response.ShouldNotBeNull();
        response.AlreadyTracked.ShouldBeFalse();

        // Romaji, and honestly so: MangaUpdates files this series under "Fremd Torturchen"
        // and the English name is one unlabelled entry among its alternative titles.
        response.TitleEnglish.ShouldBe("Fremd Torturchen");

        // Nine volumes out of "9 Volumes + 1 Extra (Complete)".
        response.VolumeCount.ShouldBe(9);

        var series = await dbContext.Series
            .Include(candidate => candidate.Volumes)
            .Include(candidate => candidate.UserState)
            .SingleAsync(candidate => candidate.Id == response.SeriesId);

        // Linked to the release source on the way in, and with no AniList id at all — which
        // is what makes the nightly refresh skip matching for it entirely.
        series.MangaUpdatesId.ShouldBe(MangaUpdatesId);
        series.AniListId.ShouldBeNull();

        series.PublicationStatus.ShouldBe(PublicationStatus.Completed);
        series.Author.ShouldBe("AYASATO Keishi");
        series.Illustrator.ShouldBe("UKAI Saki");

        // The one field AniList never supplies, so this path is the only one that fills it.
        series.Publisher.ShouldBe("Yen On");

        series.Description.ShouldNotBeNull().ShouldContain("Torturchen");
        series.CoverObjectKey.ShouldBe(MangaUpdatesCoverObjectKey);
        // Only a refresh stamps it; the add-time refresh is stubbed out here.
        series.LastSyncedAt.ShouldBeNull();

        // Volumes 1..9, all of them in print: that is what a release source's count means,
        // and it is a stronger claim than AniList's volume list supports.
        series.Volumes.Count.ShouldBe(9);
        series.Volumes.Select(volume => (int)volume.Number).Order().ShouldBe(Enumerable.Range(1, 9));
        series.Volumes.ShouldAllBe(volume => volume.IsReleased);
        series.Volumes.ShouldAllBe(volume => !volume.IsLocalOnly);
        series.Volumes.ShouldAllBe(volume => volume.ReleaseDate == null);

        series.UserState.ShouldNotBeNull().TrackingStatus.ShouldBe(TrackingStatus.PlanToStart);
    }

    [Fact]
    public async Task AddAsync_MangaUpdates_TheSameSeriesTwice_ReturnsTheExistingSeriesAndWritesNothing()
    {
        await using var dbContext = CreateDbContext();
        var mangaUpdatesSource = CreateMangaUpdatesSource();
        var service = CreateService(
            dbContext, CreateMetadataSource(), mangaUpdatesSource: mangaUpdatesSource);

        var first = await service.AddAsync(CreateMangaUpdatesRequest(), CancellationToken.None);
        var second = await service.AddAsync(CreateMangaUpdatesRequest(), CancellationToken.None);

        first.ShouldNotBeNull();
        second.ShouldNotBeNull().AlreadyTracked.ShouldBeTrue();
        second.SeriesId.ShouldBe(first.SeriesId);

        (await dbContext.Series.CountAsync()).ShouldBe(1);
        (await dbContext.Volumes.CountAsync()).ShouldBe(9);
        (await dbContext.SeriesUserStates.CountAsync()).ShouldBe(1);

        await mangaUpdatesSource.Received(1).GetSeriesAsync(MangaUpdatesId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddAsync_MangaUpdates_ASeriesTheNightlySyncAlreadyLinked_IsNotAddedTwice()
    {
        await using var dbContext = CreateDbContext();

        // The case the unique index would otherwise catch as a violation: this series was
        // added from AniList and *linked* to MangaUpdates by a later sync, so it was never
        // added from MangaUpdates — and pressing Add on its MangaUpdates row must still be a
        // polite no-op rather than a 500.
        dbContext.Series.Add(new Series
        {
            Id = Guid.CreateVersion7(),
            AniListId = 97100,
            MangaUpdatesId = MangaUpdatesId,
            TitleEnglish = "Torture Princess: Fremd Torturchen",
            Format = Format.LightNovel,
            PublicationStatus = PublicationStatus.Completed,
        });
        await dbContext.SaveChangesAsync();

        var mangaUpdatesSource = CreateMangaUpdatesSource();

        var response = await CreateService(
                dbContext, CreateMetadataSource(), mangaUpdatesSource: mangaUpdatesSource)
            .AddAsync(CreateMangaUpdatesRequest(), CancellationToken.None);

        response.ShouldNotBeNull().AlreadyTracked.ShouldBeTrue();

        // The English title the shelf already had, not the romaji one MangaUpdates would have
        // written: nothing was overwritten, because nothing was written.
        response.TitleEnglish.ShouldBe("Torture Princess: Fremd Torturchen");

        (await dbContext.Series.CountAsync()).ShouldBe(1);

        // And MangaUpdates was spared the request entirely.
        await mangaUpdatesSource.DidNotReceiveWithAnyArgs().GetSeriesAsync(default, default);
    }

    [Fact]
    public async Task AddAsync_MangaUpdates_UnknownId_ReturnsNullAndAddsNothing()
    {
        await using var dbContext = CreateDbContext();

        var mangaUpdatesSource = Substitute.For<IMangaUpdatesSeriesQueryService>();
        mangaUpdatesSource.GetSeriesAsync(Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns((MangaUpdatesSeriesMetadata?)null);

        var response = await CreateService(
                dbContext, CreateMetadataSource(), mangaUpdatesSource: mangaUpdatesSource)
            .AddAsync(CreateMangaUpdatesRequest(), CancellationToken.None);

        response.ShouldBeNull();
        (await dbContext.Series.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task AddAsync_MangaUpdates_ANewSeries_AdoptsItsStagedIsbnsThenRefreshesIt()
    {
        await using var dbContext = CreateDbContext();

        var isbnBackfill = NoStagedIsbns();
        var sync = Substitute.For<ISeriesSyncService>();
        sync.RunForSeriesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new SyncRun
            {
                Id = Guid.CreateVersion7(),
                StartedAt = DateTimeOffset.UnixEpoch,
                Summary = "Fremd Torturchen refreshed · 9 volumes enriched from OpenLibrary",
            });

        var response = await CreateService(
                dbContext, CreateMetadataSource(), isbnBackfill, sync, CreateMangaUpdatesSource())
            .AddAsync(CreateMangaUpdatesRequest(), CancellationToken.None);

        // The same completion the AniList path gets, in the same order and for the same
        // reason: enrichment is keyed on ISBN, so the ISBNs go on the volumes first.
        Received.InOrder(() =>
        {
            isbnBackfill.RunForSeriesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
            sync.RunForSeriesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        });

        response.ShouldNotBeNull().RefreshSummary.ShouldNotBeNull().ShouldContain("9 volumes enriched");
    }

    [Fact]
    public async Task DeleteAsync_ATrackedSeries_TakesItsVolumesAndAllUserStateWithIt()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, CreateMetadataSource());

        var added = await service.AddAsync(CreateRequest(), CancellationToken.None);
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
    public async Task DeleteAsync_ASeriesWithVolumeCovers_DeletesThemAndKeepsTheSeriesArt()
    {
        await using var dbContext = CreateDbContext();
        var coverStore = Substitute.For<ICoverStore>();
        var service = CreateService(dbContext, CreateMetadataSource(), coverStore: coverStore);
        var seriesId = (await service.AddAsync(CreateRequest(), CancellationToken.None)).ShouldNotBeNull().SeriesId;

        var volumes = await dbContext.Volumes.OrderBy(volume => volume.Number).Take(3).ToListAsync();
        var uploaded = UploadKey(volumes[0].Id);
        var alsoUploaded = UploadKey(volumes[2].Id);
        var syncedArt = LnShelf.Api.Features.Covers.Models.CoverObjectKey.ForVolumeSync(volumes[1].Id, "OpenLibrary", ".jpg");

        volumes[0].UserState = new VolumeUserState { VolumeId = volumes[0].Id, CoverOverrideObjectKey = uploaded };
        volumes[1].CoverObjectKey = syncedArt;
        volumes[2].UserState = new VolumeUserState { VolumeId = volumes[2].Id, CoverOverrideObjectKey = alsoUploaded };
        await dbContext.SaveChangesAsync();

        (await service.DeleteAsync(seriesId, CancellationToken.None)).ShouldBeTrue();

        // Per-volume keys name volume ids a re-added copy will not have, so they go: the
        // user's uploads and the synced volume art alike. The series art is shared with a
        // re-added copy, and stays.
        await coverStore.Received(1).DeleteAsync(uploaded, Arg.Any<CancellationToken>());
        await coverStore.Received(1).DeleteAsync(alsoUploaded, Arg.Any<CancellationToken>());
        await coverStore.Received(1).DeleteAsync(syncedArt, Arg.Any<CancellationToken>());
        await coverStore.DidNotReceive().DeleteAsync(CoverObjectKey, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_TheStoreRefusesAnUploadDelete_StillDeletesTheSeries()
    {
        await using var dbContext = CreateDbContext();
        var coverStore = Substitute.For<ICoverStore>();
        coverStore.DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("The cover store is down")));
        var service = CreateService(dbContext, CreateMetadataSource(), coverStore: coverStore);
        var seriesId = (await service.AddAsync(CreateRequest(), CancellationToken.None)).ShouldNotBeNull().SeriesId;

        var volume = await dbContext.Volumes.FirstAsync();
        volume.UserState = new VolumeUserState { VolumeId = volume.Id, CoverOverrideObjectKey = UploadKey(volume.Id) };
        await dbContext.SaveChangesAsync();

        // The rows are already gone when the store is asked, so its failure is only an object
        // the cover-orphan-sweep will remove, not a reason to say the delete did not happen.
        (await service.DeleteAsync(seriesId, CancellationToken.None)).ShouldBeTrue();
        (await dbContext.Series.CountAsync()).ShouldBe(0);
    }

    // Spelled out because this class's CoverObjectKey constant is a different thing.
    private static string UploadKey(Guid volumeId) =>
        LnShelf.Api.Features.Covers.Models.CoverObjectKey.ForVolumeOverride(volumeId, Guid.CreateVersion7(), ".webp");

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

        var added = await service.AddAsync(CreateRequest(), CancellationToken.None);
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

        var added = await service.AddAsync(CreateRequest(), CancellationToken.None);

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

    [Fact]
    public async Task AddAsync_ASourceNoFlowIsRegisteredFor_IsRefusedRatherThanRoutedElsewhere()
    {
        await using var dbContext = CreateDbContext();

        // The regression this guards: the controller used to switch on the source with a
        // `_ =>` falling through to AniList, so a request naming a source nothing could add
        // from added the wrong series instead of saying so.
        var pipeline = new AddSeriesPipeline(
            dbContext,
            [],
            NoStagedIsbns(),
            NoRefresh(),
            TestUsers.Limits(dbContext), TimeProvider.System, NullLogger<AddSeriesPipeline>.Instance);

        var act = async () => await pipeline.RunAsync(CreateRequest(), CancellationToken.None);

        await act.ShouldThrowAsync<InvalidInputException>();
        (await dbContext.Series.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task SetStateAsync_ATrackingStatusAndANote_AreStoredAndComeBackOnTheDetail()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, CreateMetadataSource());
        var added = await service.AddAsync(CreateRequest(), CancellationToken.None);

        var detail = await service.SetStateAsync(
            added.ShouldNotBeNull().SeriesId,
            new UpdateSeriesStateRequest
            {
                TrackingStatus = TrackingStatus.Reading,
                Notes = "Waiting on the Yen On reprint of volume 4.",
            },
            CancellationToken.None);

        detail.ShouldNotBeNull().TrackingStatus.ShouldBe(TrackingStatus.Reading);
        detail.Notes.ShouldBe("Waiting on the Yen On reprint of volume 4.");

        var state = await dbContext.SeriesUserStates.SingleAsync();
        state.TrackingStatus.ShouldBe(TrackingStatus.Reading);
        state.Notes.ShouldBe("Waiting on the Yen On reprint of volume 4.");
    }

    /// <summary>
    /// Partial update: every omitted field is left alone, so the status dropdown and the
    /// note box can each save only what they changed.
    /// </summary>
    [Fact]
    public async Task SetStateAsync_AFieldLeftNull_LeavesWhatWasThereAlone()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, CreateMetadataSource());
        var seriesId = (await service.AddAsync(CreateRequest(), CancellationToken.None))
            .ShouldNotBeNull().SeriesId;

        await service.SetStateAsync(
            seriesId,
            new UpdateSeriesStateRequest { TrackingStatus = TrackingStatus.Reading, Notes = "Re-reading." },
            CancellationToken.None);

        // Only the status this time; the note must survive it.
        var detail = await service.SetStateAsync(
            seriesId,
            new UpdateSeriesStateRequest { TrackingStatus = TrackingStatus.Completed },
            CancellationToken.None);

        detail.ShouldNotBeNull().TrackingStatus.ShouldBe(TrackingStatus.Completed);
        detail.Notes.ShouldBe("Re-reading.");
    }

    /// <summary>
    /// With null meaning "unchanged", the empty string is how a note is erased — and a
    /// blank note already reads as no note on the way out.
    /// </summary>
    [Fact]
    public async Task SetStateAsync_AnEmptyNote_ClearsIt()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, CreateMetadataSource());
        var seriesId = (await service.AddAsync(CreateRequest(), CancellationToken.None))
            .ShouldNotBeNull().SeriesId;

        await service.SetStateAsync(
            seriesId, new UpdateSeriesStateRequest { Notes = "Lend to Anders." }, CancellationToken.None);

        var detail = await service.SetStateAsync(
            seriesId, new UpdateSeriesStateRequest { Notes = string.Empty }, CancellationToken.None);

        detail.ShouldNotBeNull().Notes.ShouldBeNull();
    }

    [Fact]
    public async Task SetStateAsync_ASeriesThatWasNeverTouched_GetsItsUserStateRow()
    {
        await using var dbContext = CreateDbContext();

        // An imported or hand-seeded series can reach the shelf with no user-state row at
        // all; setting its status is exactly the touch that row records.
        var series = new Series
        {
            Id = Guid.CreateVersion7(),
            AniListId = 999,
            TitleEnglish = "Overlord",
            Format = Format.LightNovel,
            PublicationStatus = PublicationStatus.Ongoing,
        };
        dbContext.Series.Add(series);
        await dbContext.SaveChangesAsync();

        var detail = await CreateService(dbContext, CreateMetadataSource()).SetStateAsync(
            series.Id,
            new UpdateSeriesStateRequest { TrackingStatus = TrackingStatus.Dropped },
            CancellationToken.None);

        detail.ShouldNotBeNull().TrackingStatus.ShouldBe(TrackingStatus.Dropped);
        (await dbContext.SeriesUserStates.SingleAsync()).TrackingStatus.ShouldBe(TrackingStatus.Dropped);
    }

    [Fact]
    public async Task SetStateAsync_UnknownSeries_ReturnsNullAndWritesNothing()
    {
        await using var dbContext = CreateDbContext();

        var detail = await CreateService(dbContext, CreateMetadataSource()).SetStateAsync(
            Guid.CreateVersion7(),
            new UpdateSeriesStateRequest { TrackingStatus = TrackingStatus.Reading },
            CancellationToken.None);

        detail.ShouldBeNull();
        (await dbContext.SeriesUserStates.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task MarkSeenAsync_ASeriesWithTheBadgeUp_PutsItDown()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, CreateMetadataSource());
        var seriesId = (await service.AddAsync(CreateRequest(), CancellationToken.None))
            .ShouldNotBeNull().SeriesId;

        // What sync does when a volume of an already-synced series flips to released.
        (await dbContext.SeriesUserStates.SingleAsync()).HasNewVolume = true;
        await dbContext.SaveChangesAsync();

        (await service.MarkSeenAsync(seriesId, CancellationToken.None)).ShouldBeTrue();

        (await dbContext.SeriesUserStates.SingleAsync()).HasNewVolume.ShouldBeFalse();
    }

    [Fact]
    public async Task MarkSeenAsync_ABadgeThatIsAlreadyDown_IsStillASuccess()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, CreateMetadataSource());
        var seriesId = (await service.AddAsync(CreateRequest(), CancellationToken.None))
            .ShouldNotBeNull().SeriesId;

        // The common case by far: the page calls this on every visit, and most visits are
        // to a series with nothing new waiting.
        (await service.MarkSeenAsync(seriesId, CancellationToken.None)).ShouldBeTrue();
        (await dbContext.SeriesUserStates.SingleAsync()).HasNewVolume.ShouldBeFalse();
    }

    [Fact]
    public async Task MarkSeenAsync_ASeriesWithNoUserStateRow_SucceedsWithoutWritingOne()
    {
        await using var dbContext = CreateDbContext();

        var series = new Series
        {
            Id = Guid.CreateVersion7(),
            AniListId = 999,
            TitleEnglish = "Overlord",
            Format = Format.LightNovel,
            PublicationStatus = PublicationStatus.Ongoing,
        };
        dbContext.Series.Add(series);
        await dbContext.SaveChangesAsync();

        (await CreateService(dbContext, CreateMetadataSource())
            .MarkSeenAsync(series.Id, CancellationToken.None)).ShouldBeTrue();

        // No row means the badge is already down, so acknowledging it must not invent one
        // — a visit is not a preference.
        (await dbContext.SeriesUserStates.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task MarkSeenAsync_UnknownSeries_IsFalseRatherThanAFailure()
    {
        await using var dbContext = CreateDbContext();

        (await CreateService(dbContext, CreateMetadataSource())
            .MarkSeenAsync(Guid.CreateVersion7(), CancellationToken.None)).ShouldBeFalse();
    }

    /// <summary>
    /// The duplicate the MangaUpdates fallback used to create: added while AniList was
    /// down, then added again from AniList once it was back.
    /// </summary>
    [Fact]
    public async Task AddAsync_AniList_ASeriesAddedFromMangaUpdatesUnderTheSameTitle_LinksItInsteadOfAddingACopy()
    {
        await using var dbContext = CreateDbContext();

        // MangaUpdates filed it under the romaji title, which is AniList's second one.
        var existing = new Series
        {
            Id = Guid.CreateVersion7(),
            MangaUpdatesId = MangaUpdatesId,
            TitleEnglish = "Mushoku Tensei: Isekai Ittara Honki Dasu",
            Format = Format.LightNovel,
            PublicationStatus = PublicationStatus.Completed,
        };
        var volume = new Volume { Id = Guid.CreateVersion7(), Number = 1, IsReleased = true };
        volume.UserState = new VolumeUserState { VolumeId = volume.Id, ReadStatus = ReadStatus.Read, Owned = true };
        existing.Volumes.Add(volume);
        dbContext.Series.Add(existing);
        await dbContext.SaveChangesAsync();

        var response = await CreateService(dbContext, CreateMetadataSource())
            .AddAsync(CreateRequest(), CancellationToken.None);

        response.ShouldNotBeNull().SeriesId.ShouldBe(existing.Id);
        response.AlreadyTracked.ShouldBeTrue();

        // One row, now linked to both catalogues, with the user's state where it was.
        var series = await dbContext.Series.SingleAsync();
        series.AniListId.ShouldBe(AniListId);
        series.MangaUpdatesId.ShouldBe(MangaUpdatesId);
        (await dbContext.VolumeUserStates.SingleAsync()).ReadStatus.ShouldBe(ReadStatus.Read);
    }

    [Fact]
    public async Task AddAsync_AniList_TheSameTitleAsAMangaUpdatesMangaRow_StillAddsTheLightNovel()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Series.Add(new Series
        {
            Id = Guid.CreateVersion7(),
            MangaUpdatesId = MangaUpdatesId,
            TitleEnglish = "Mushoku Tensei: Jobless Reincarnation",
            Format = Format.Manga,
            PublicationStatus = PublicationStatus.Ongoing,
        });
        await dbContext.SaveChangesAsync();

        var response = await CreateService(dbContext, CreateMetadataSource())
            .AddAsync(CreateRequest(), CancellationToken.None);

        // Same title, different format: the manga and the light novel are two series.
        response.ShouldNotBeNull().AlreadyTracked.ShouldBeFalse();
        (await dbContext.Series.CountAsync()).ShouldBe(2);
    }

    [Theory]
    [InlineData(0L, null)]
    [InlineData(-5L, null)]
    [InlineData(null, 0L)]
    [InlineData(null, -5L)]
    public async Task AddAsync_ANonPositiveId_IsA400RatherThanA500(long? aniListId, long? mangaUpdatesId)
    {
        await using var dbContext = CreateDbContext();
        var request = new AddSeriesRequest
        {
            AniListId = aniListId is null ? null : (int)aniListId,
            MangaUpdatesId = mangaUpdatesId,
            Format = Format.LightNovel,
        };

        var act = async () => await CreateService(dbContext, CreateMetadataSource())
            .AddAsync(request, CancellationToken.None);

        await act.ShouldThrowAsync<InvalidInputException>();
    }

    [Fact]
    public async Task SetStateAsync_ANoteLongerThanTheColumn_IsRefused()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, CreateMetadataSource());
        var seriesId = (await service.AddAsync(CreateRequest(), CancellationToken.None))
            .ShouldNotBeNull().SeriesId;

        var act = async () => await service.SetStateAsync(
            seriesId,
            new UpdateSeriesStateRequest { Notes = new string('x', SeriesUserState.NotesMaximumLength + 1) },
            CancellationToken.None);

        await act.ShouldThrowAsync<InvalidInputException>();
    }

    [Fact]
    public async Task SetStateAsync_ANoteExactlyAsLongAsTheColumn_IsStored()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, CreateMetadataSource());
        var seriesId = (await service.AddAsync(CreateRequest(), CancellationToken.None))
            .ShouldNotBeNull().SeriesId;

        var detail = await service.SetStateAsync(
            seriesId,
            new UpdateSeriesStateRequest { Notes = new string('x', SeriesUserState.NotesMaximumLength) },
            CancellationToken.None);

        detail.ShouldNotBeNull().Notes.ShouldNotBeNull().Length.ShouldBe(SeriesUserState.NotesMaximumLength);
    }

    [Fact]
    public async Task SetVolumeViewAsync_ASeriesWithNoUserStateRow_StaysPlanToStart()
    {
        await using var dbContext = CreateDbContext();
        var series = new Series
        {
            Id = Guid.CreateVersion7(),
            AniListId = 999,
            TitleEnglish = "Overlord",
            Format = Format.LightNovel,
            PublicationStatus = PublicationStatus.Ongoing,
        };
        dbContext.Series.Add(series);
        await dbContext.SaveChangesAsync();

        await CreateService(dbContext, CreateMetadataSource()).SetVolumeViewAsync(
            series.Id, new SetSeriesVolumeViewRequest { View = VolumeView.List }, CancellationToken.None);

        // A missing row reads as PlanToStart; creating it to hold a view preference must
        // not quietly turn that into Reading, the enum's default.
        (await dbContext.SeriesUserStates.SingleAsync()).TrackingStatus.ShouldBe(TrackingStatus.PlanToStart);
    }

    /// <summary>
    /// The service wired with both real add flows — the composition the app runs.
    /// </summary>
    // The flows are the real implementations rather than substitutes, because they are
    // what the source-specific half of an add *is*: which id names the series, which row
    // already holds it, and what gets built. Substituting them would leave these tests
    // asserting only the pipeline, which is the half that was never in doubt.
    private static SeriesCommandService CreateService(
        LnShelfDbContext dbContext,
        ISeriesMetadataSource metadataSource,
        IIsbnBackfill? isbnBackfill = null,
        ISeriesSyncService? seriesSync = null,
        IMangaUpdatesSeriesQueryService? mangaUpdatesSource = null,
        ICoverStore? coverStore = null,
        IReleaseSourceMatcher? releaseSourceMatcher = null) =>
        new(dbContext,
            new AddSeriesPipeline(
                dbContext,
                [
                    new AniListAddSeriesFlow(
                        metadataSource,
                        CreateCoverCache(),
                        new AddTimeReleaseLink(
                            releaseSourceMatcher ?? NoReleaseLink(),
                            dbContext,
                            NullLogger<AddTimeReleaseLink>.Instance)),
                    new MangaUpdatesAddSeriesFlow(
                        mangaUpdatesSource ?? Substitute.For<IMangaUpdatesSeriesQueryService>(),
                        CreateSeriesCoverCache(),
                        NullLogger<MangaUpdatesAddSeriesFlow>.Instance),
                ],
                isbnBackfill ?? NoStagedIsbns(),
                seriesSync ?? NoRefresh(),
                TestUsers.Limits(dbContext), TimeProvider.System, NullLogger<AddSeriesPipeline>.Instance),
            coverStore ?? Substitute.For<ICoverStore>(),
            TimeProvider.System,
            NullLogger<SeriesCommandService>.Instance);

    /// <summary>The release source knows nothing of this series, so AniList's volumes stand.</summary>
    internal static IReleaseSourceMatcher NoReleaseLink()
    {
        var matcher = Substitute.For<IReleaseSourceMatcher>();
        matcher.MatchAsync(Arg.Any<Series>(), Arg.Any<IReadOnlySet<long>>(), Arg.Any<CancellationToken>())
            .Returns(new ReleaseSourceMatch
            {
                Outcome = ReleaseMatchOutcome.NoCandidate,
                Explanation = "Nothing to link in this test.",
            });

        return matcher;
    }

    /// <summary>Staging holds nothing for this series — the usual case for a new one.</summary>
    internal static IIsbnBackfill NoStagedIsbns()
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
    private static ISeriesCoverCache CreateCoverCache()
    {
        var coverCache = Substitute.For<ISeriesCoverCache>();
        coverCache.EnsureCachedAsync(
                MetadataSourceName.AniList,
                AniListId,
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(CoverObjectKey);

        return coverCache;
    }

    /// <inheritdoc cref="CreateCoverCache"/>
    private static ISeriesCoverCache CreateSeriesCoverCache()
    {
        var coverCache = Substitute.For<ISeriesCoverCache>();
        coverCache.EnsureCachedAsync(
                MetadataSourceName.MangaUpdates,
                MangaUpdatesId,
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(MangaUpdatesCoverObjectKey);

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

    private static IMangaUpdatesSeriesQueryService CreateMangaUpdatesSource()
    {
        var source = Substitute.For<IMangaUpdatesSeriesQueryService>();
        source.GetSeriesAsync(MangaUpdatesId, Arg.Any<CancellationToken>())
            .Returns(CreateMangaUpdatesMetadata());

        return source;
    }

    /// <summary>
    /// The shape MangaUpdatesSeriesMapper produces from the captured Fremd Torturchen
    /// record — nine volumes from a status of "9 Volumes + 1 Extra (Complete)".
    /// </summary>
    private static MangaUpdatesSeriesMetadata CreateMangaUpdatesMetadata() => new()
    {
        SourceId = MangaUpdatesId,
        TitleEnglish = "Fremd Torturchen",
        Format = Format.LightNovel,
        PublicationStatus = PublicationStatus.Completed,
        StartYear = 2016,
        Description = "\"I am 'Torturchen' Elisabeth Le Fanu, both a proud wolf and a lowly sow.\"",
        CoverImageUrl = "https://cdn.mangaupdates.com/image/i289956.jpg",
        Genres = ["Action", "Drama", "Fantasy"],
        Author = "AYASATO Keishi",
        Illustrator = "UKAI Saki",
        Publisher = "Yen On",
        VolumeCount = 9,
        RawStatus = "9 Volumes + 1 Extra (Complete)",
    };

    private static AddSeriesRequest CreateRequest() =>
        new() { AniListId = AniListId, Format = Format.LightNovel };

    private static AddSeriesRequest CreateMangaUpdatesRequest() =>
        new() { MangaUpdatesId = MangaUpdatesId, Format = Format.LightNovel };

    private static LnShelfDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<LnShelfDbContext>()
            .UseInMemoryDatabase($"shelf-{Guid.CreateVersion7()}")
            .Options, TestUsers.Current);
}
