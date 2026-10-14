using LnShelf.Api.Tests.Features.Metadata.Fixtures;
using static LnShelf.Api.Tests.Features.Metadata.Services.VolumeEnrichmentTestContext;

namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// The owner's report, end to end through a refresh: 86's Vol. 14 is on Apple Books as a
/// pre-order while MangaUpdates still counts 13, and the shelf should know about it.
/// </summary>
// The store answers with its captured response (Vol. 14 dated 2026-12-08). The release source
// is a substitute stating thirteen, which is what MangaUpdates says today; it still owns the
// count, so 14 arrives as an announced volume rather than one in print.
public sealed class SeriesRefresherNextVolumeTests
{
    private const long MangaUpdatesId = 15180124327;

    private static readonly DateTimeOffset Now = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Volume14Date = new(2026, 12, 8);

    [Fact]
    public async Task RefreshAsync_TheOwnersThirteenVolumeShelf_GainsTheAnnouncedFourteen()
    {
        var series = OwnersShelf();
        var volumesBefore = Facts(series);

        var (run, events) = await RefreshAsync(CreateRefresher(new MutableTimeProvider(Now)), series);

        var volume14 = series.Volumes.Single(volume => volume.Number == 14);
        volume14.IsReleased.ShouldBeFalse();
        volume14.ReleaseDate.ShouldBe(Volume14Date);
        volume14.ReleaseDateSource.ShouldBe(MetadataSourceName.AppleBooks);
        run.NewVolumes.ShouldBe(1);

        // News, unlike an extra: announced now, with its date.
        var announced = events.ShouldHaveSingleItem();
        (announced.Kind, announced.VolumeNumber).ShouldBe((ShelfEventKind.VolumeAnnounced, 14m));
        announced.NewDate.ShouldBe(Volume14Date);

        // A pre-order does not raise the badge: the pill names the newest volume that is out.
        series.UserState.ShouldNotBeNull().HasNewVolume.ShouldBeFalse();

        // Nothing else moved, and the count is still the release source's.
        Facts(series).Where(facts => facts.Number <= 13).ShouldBe(volumesBefore);
        series.ReleaseSourceVolumeCount.ShouldBe(13);
    }

    [Fact]
    public async Task RefreshAsync_RunTwice_ChangesNothingTheSecondTime()
    {
        var series = OwnersShelf();
        var refresher = CreateRefresher(new MutableTimeProvider(Now));

        await RefreshAsync(refresher, series);
        var volumesAfterFirst = Facts(series);

        var (second, events) = await RefreshAsync(refresher, series);

        // testing.md #3: the same sync twice changes nothing.
        Facts(series).ShouldBe(volumesAfterFirst);
        second.NewVolumes.ShouldBe(0);
        events.ShouldBeEmpty();

        // The shelf's 14 against MangaUpdates' 13 is recorded every run, resolved rather than
        // waiting for anyone.
        second.Discrepancies.ShouldContain(discrepancy => discrepancy.Field == SyncDiscrepancyField.VolumeCount);
        second.Discrepancies.ShouldAllBe(discrepancy => discrepancy.ResolvedWith != null);
    }

    [Fact]
    public async Task RefreshAsync_TheNightItsDatePasses_RaisesTheBadge()
    {
        // The owner's amendment: a pre-order raises the badge the night it comes out, even
        // though MangaUpdates still counts 13 and the count never flips it.
        var series = OwnersShelf();
        var clock = new MutableTimeProvider(Now);
        var refresher = CreateRefresher(clock);

        await RefreshAsync(refresher, series);
        series.UserState.ShouldNotBeNull().HasNewVolume.ShouldBeFalse();

        clock.UtcNow = new DateTimeOffset(Volume14Date.AddDays(1), new TimeOnly(4, 0), TimeSpan.Zero);
        var (_, events) = await RefreshAsync(refresher, series);

        var released = events.ShouldHaveSingleItem();
        (released.Kind, released.VolumeNumber).ShouldBe((ShelfEventKind.VolumeReleased, 14m));
        series.UserState.HasNewVolume.ShouldBeTrue();
        series.Volumes.Single(volume => volume.Number == 14).IsReleased.ShouldBeFalse();
    }

    [Fact]
    public async Task RefreshAsync_TheFirstRefreshAfterAdding_CreatesTheVolumeWithoutNews()
    {
        // What adding does: the count writer sets 13 first, then the catalogue pass runs. A
        // first refresh is not news, so no event and no badge.
        var series = OwnersShelf();
        series.LastSyncedAt = null;
        series.ReleaseSourceVolumeCount = null;

        var (run, events) = await RefreshAsync(CreateRefresher(new MutableTimeProvider(Now)), series);

        series.Volumes.ShouldContain(volume => volume.Number == 14);
        run.NewVolumes.ShouldBe(1);
        events.ShouldBeEmpty();
        series.UserState.ShouldNotBeNull().HasNewVolume.ShouldBeFalse();
    }

    [Fact]
    public async Task RefreshAsync_TheFirstRefreshWithTheNextVolumeAlreadyOut_RaisesNoBadge()
    {
        var series = OwnersShelf();
        series.LastSyncedAt = null;

        await RefreshAsync(CreateRefresher(new MutableTimeProvider(Now), OutAlreadyCatalogue()), series);

        series.Volumes.ShouldContain(volume => volume.Number == 14);
        series.UserState.ShouldNotBeNull().HasNewVolume.ShouldBeFalse();
    }

    [Fact]
    public async Task RefreshAsync_TheNextVolumeIsAlreadyOut_RaisesTheBadge()
    {
        var series = OwnersShelf();

        var (_, events) = await RefreshAsync(
            CreateRefresher(new MutableTimeProvider(Now), OutAlreadyCatalogue()), series);

        var released = events.ShouldHaveSingleItem();
        (released.Kind, released.VolumeNumber).ShouldBe((ShelfEventKind.VolumeReleased, 14m));
        series.UserState.ShouldNotBeNull().HasNewVolume.ShouldBeTrue();
    }

    [Fact]
    public async Task RefreshAsync_TheRecordGoneWhileAKnownVolumeComesOut_DoesNotRaiseTheBadgeEveryNight()
    {
        // The MangaUpdates record was removed after the last sync, so LastSyncedAt stands
        // still and Vol. 14's date passing is seen again on every refresh. Clearing the badge
        // must stick.
        var series = OwnersShelf();
        series.Volumes.Add(new Volume
        {
            Id = Guid.CreateVersion7(),
            Number = 14,
            ReleaseDate = Volume14Date,
            ReleaseDateSource = MetadataSourceName.AppleBooks,
        });
        var clock = new MutableTimeProvider(new DateTimeOffset(Volume14Date.AddDays(1), new TimeOnly(4, 0), TimeSpan.Zero));
        var refresher = CreateRefresher(clock, recordGone: true);

        foreach (var night in Enumerable.Range(0, 2))
        {
            clock.UtcNow = clock.UtcNow.AddDays(night);
            series.UserState!.HasNewVolume = false;

            var (_, events) = await RefreshAsync(refresher, series);

            events.ShouldContain(shelfEvent => shelfEvent.Kind == ShelfEventKind.VolumeReleased);
            series.UserState.HasNewVolume.ShouldBeFalse();
        }
    }

    [Fact]
    public async Task RefreshAsync_UnderAVolumeCountChoice_CreatesNothing()
    {
        // The user settled the count by hand; the store does not reopen it.
        var series = OwnersShelf();
        series.SourceChoices.Add(new SourceChoice
        {
            Id = Guid.CreateVersion7(),
            SeriesId = series.Id,
            Field = SyncDiscrepancyField.VolumeCount,
            Source = MetadataSourceName.MangaUpdates,
            ChosenAt = DateTimeOffset.UnixEpoch,
        });

        var (run, _) = await RefreshAsync(CreateRefresher(new MutableTimeProvider(Now)), series);

        series.Volumes.Count.ShouldBe(13);
        run.NewVolumes.ShouldBe(0);
    }

    [Fact]
    public async Task RefreshAsync_TheCountCatchesUp_ReleasesTheStoresVolumeByCount()
    {
        var series = OwnersShelf();
        await RefreshAsync(CreateRefresher(new MutableTimeProvider(Now)), series);

        await RefreshAsync(CreateRefresher(new MutableTimeProvider(Now.AddDays(1)), volumeCount: 14), series);

        // The existing path: the count flips it, and the count raises the badge.
        series.Volumes.Count(volume => volume.Number == 14).ShouldBe(1);
        series.Volumes.Single(volume => volume.Number == 14).IsReleased.ShouldBeTrue();
        series.UserState.ShouldNotBeNull().HasNewVolume.ShouldBeTrue();
    }

    private static async Task<(SyncRun Run, IReadOnlyList<ShelfEvent> Events)> RefreshAsync(
        SeriesRefresher refresher,
        Series series)
    {
        var run = new SyncRun { Id = Guid.CreateVersion7(), StartedAt = Now };

        var events = await refresher.RefreshAsync(series, run, [], new EnrichmentBudget(8, 8), CancellationToken.None);

        return (run, events);
    }

    private static SeriesRefresher CreateRefresher(
        TimeProvider timeProvider,
        IVolumeCatalogueSource? catalogue = null,
        int volumeCount = 13,
        bool recordGone = false)
    {
        var releaseSource = Substitute.For<IReleaseSource>();
        releaseSource.SourceName.Returns("MangaUpdates");
        releaseSource.GetSeriesAsync(MangaUpdatesId, Arg.Any<CancellationToken>())
            .Returns(recordGone ? null : new ReleaseSeriesInfo
            {
                ExternalId = MangaUpdatesId,
                Title = "86―EIGHTY-SIX",
                VolumeCount = volumeCount,
                ExtraVolumeCount = 0,
                PublicationStatus = PublicationStatus.Ongoing,
                RawStatus = $"{volumeCount} Volumes (Ongoing)",
            });

        return new SeriesRefresher(
            Substitute.For<IReleaseSourceMatcher>(),
            releaseSource,
            new SeriesDescriptionHealer(Substitute.For<ISeriesMetadataSource>(), NullLogger<SeriesDescriptionHealer>.Instance),
            new ReleaseCountWriter(releaseSource, NullLogger<ReleaseCountWriter>.Instance),
            new PublicationStatusWriter(releaseSource, NullLogger<PublicationStatusWriter>.Instance),
            VolumeEnrichmentPipeline.Create(
                catalogue ?? Catalogue([.. AppleBooksFixtures.LoadCatalogue(AppleBooksFixtures.EightySixSearch)]),
                NoEnrichment(),
                NoCandidates(),
                VolumeEnrichmentPipeline.Writer(CoverCache())),
            ChapterMapPipeline.NoSources(),
            timeProvider);
    }

    /// <summary>
    /// The store as it would read once Vol. 14 is out: Vols. 1 and 8 so the title is
    /// recognised, and 14 dated last month.
    /// </summary>
    private static IVolumeCatalogueSource OutAlreadyCatalogue() => Catalogue(
        Record(1, new DateOnly(2019, 3, 26)),
        Record(8, new DateOnly(2021, 8, 24)),
        Record(14, new DateOnly(2026, 9, 1)));

    private static CatalogueVolume Record(int number, DateOnly releaseDate) => new()
    {
        SourceRecordKey = $"light-novel-{number}",
        Title = $"86--EIGHTY-SIX, Vol. {number} (light novel)",
        ReleaseDate = releaseDate,
    };

    /// <summary>
    /// The owner's series as the nightly sweep left it: thirteen volumes in print by the count,
    /// already described by Open Library, one of them owned, swept yesterday.
    /// </summary>
    private static Series OwnersShelf()
    {
        var series = new Series
        {
            Id = Guid.CreateVersion7(),
            AniListId = 101172,
            MangaUpdatesId = MangaUpdatesId,
            TitleEnglish = "86―EIGHTY-SIX",
            Format = Format.LightNovel,
            PublicationStatus = PublicationStatus.Ongoing,
            Description = "The Republic of San Magnolia has long been under attack.",
            ReleaseSourceVolumeCount = 13,
            LastSyncedAt = Now.AddDays(-1),
            UserState = new SeriesUserState { TrackingStatus = TrackingStatus.Reading },
        };

        foreach (var number in Enumerable.Range(1, 13))
        {
            series.Volumes.Add(new Volume
            {
                Id = Guid.CreateVersion7(),
                Number = number,
                IsReleased = true,
                ReleaseDate = new DateOnly(2019, 3, 26).AddMonths(number * 4),
                ReleaseDateSource = MetadataSourceName.OpenLibrary,
                Title = string.Empty,
                Description = $"Volume {number}, from Open Library.",
                CoverObjectKey = $"volume/openlibrary/{number}.jpg",
            });
        }

        var owned = series.Volumes.Single(volume => volume.Number == 3);
        owned.UserState = new VolumeUserState { VolumeId = owned.Id, ReadStatus = ReadStatus.Read, Owned = true };

        return series;
    }

    private static List<VolumeFactsSnapshot> Facts(Series series) =>
    [
        .. series.Volumes
            .OrderBy(volume => volume.Number)
            .Select(volume => new VolumeFactsSnapshot(
                volume.Number,
                volume.IsReleased,
                volume.ReleaseDate,
                volume.ReleaseDateSource,
                volume.Title,
                volume.Description,
                volume.CoverObjectKey,
                volume.IsLocalOnly,
                volume.UserState?.Owned)),
    ];

    private sealed record VolumeFactsSnapshot(
        decimal Number,
        bool IsReleased,
        DateOnly? ReleaseDate,
        MetadataSourceName? ReleaseDateSource,
        string? Title,
        string? Description,
        string? CoverObjectKey,
        bool IsLocalOnly,
        bool? Owned);

    /// <summary>A clock a test can move to the next night.</summary>
    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;

        public override DateTimeOffset GetUtcNow() => UtcNow;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
