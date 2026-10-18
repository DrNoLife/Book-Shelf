using LnShelf.Api.Tests.Features.Metadata.Fixtures;
using static LnShelf.Api.Tests.Features.Metadata.Services.VolumeEnrichmentTestContext;

namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// The owner's report, end to end through one refresh: "Solo Leveling should have 15 volumes.
/// Search shows 15, but when added to the shelf it's 13."
/// </summary>
// Both sources answer with captured responses: MangaUpdates' record ("13+2 Volumes" from IZE
// Press) and Apple's search with the app's own query shape, which lists "Vol. 1 (comic)" to
// "Vol. 15 (comic)". The release source still owns the main count of thirteen; the store fills
// in the two extras it counts beside them.
public sealed class SeriesRefresherWholeExtraVolumeTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RefreshAsync_TheOwnersThirteenVolumeShelf_GainsFourteenAndFifteenAndNothingElse()
    {
        var series = OwnersShelf();
        var volumesBefore = Facts(series);
        var refresher = CreateRefresher();

        var (run, events) = await RefreshAsync(refresher, series);

        series.Volumes.Select(volume => volume.Number).Order()
            .ShouldBe(Enumerable.Range(1, 15).Select(number => (decimal)number));
        run.NewVolumes.ShouldBe(2);

        var volume14 = series.Volumes.Single(volume => volume.Number == 14);
        volume14.ReleaseDate.ShouldBe(new DateOnly(2025, 12, 30));
        volume14.ReleaseDateSource.ShouldBe(MetadataSourceName.AppleBooks);
        volume14.CoverObjectKey.ShouldNotBeNull();
        volume14.IsLocalOnly.ShouldBeFalse();

        // Out by its date, which is how the projections read it; the count of thirteen never
        // called it in print and still does not.
        volume14.IsReleased.ShouldBeFalse();

        series.Volumes.Single(volume => volume.Number == 15).ReleaseDate.ShouldBe(new DateOnly(2026, 3, 17));

        // Nothing else moved: volumes 1 to 13, the reader's state, the badge only the count raises.
        Facts(series).Where(facts => facts.Number <= 13).ShouldBe(volumesBefore);
        series.Volumes.Single(volume => volume.Number == 3).UserState.ShouldNotBeNull().Owned.ShouldBeTrue();
        series.UserState.ShouldNotBeNull().HasNewVolume.ShouldBeFalse();
        series.ReleaseSourceVolumeCount.ShouldBe(13);

        // A shelf that had stopped at 13 hears about 14 and 15 once, as out.
        events.Select(shelfEvent => (shelfEvent.Kind, shelfEvent.VolumeNumber)).ShouldBe(
        [
            (ShelfEventKind.VolumeReleased, 14m),
            (ShelfEventKind.VolumeReleased, 15m),
        ]);

        // Recorded, not waiting for anyone: the shelf now tracks 15 against MangaUpdates' 13.
        run.Discrepancies.ShouldAllBe(discrepancy => discrepancy.ResolvedWith != null);
    }

    [Fact]
    public async Task RefreshAsync_RunTwice_ChangesNothingTheSecondTime()
    {
        var series = OwnersShelf();
        var refresher = CreateRefresher();

        await RefreshAsync(refresher, series);
        var volumesAfterFirst = Facts(series);

        var (second, events) = await RefreshAsync(refresher, series);

        // testing.md #3: the same sync twice changes nothing.
        Facts(series).ShouldBe(volumesAfterFirst);
        second.NewVolumes.ShouldBe(0);
        second.DatesMoved.ShouldBe(0);
        second.VolumeCoversCached.ShouldBe(0);
        second.DescriptionsWritten.ShouldBe(0);
        second.VolumesMarkedReleased.ShouldBe(0);
        events.ShouldBeEmpty();
    }

    [Fact]
    public async Task RefreshAsync_TheFirstRefreshAfterAdding_EndsWithFifteenVolumesAndNoNews()
    {
        // What adding does: AddTimeReleaseLink sizes the shelf by MangaUpdates' thirteen, and
        // the refresh that follows the add runs the catalogue pass. A first refresh is not news.
        var series = new Series
        {
            Id = Guid.CreateVersion7(),
            AniListId = 105398,
            MangaUpdatesId = MangaUpdatesFixtures.SoloLevelingSeriesId,
            TitleEnglish = "Solo Leveling",
            Format = Format.Manga,
            PublicationStatus = PublicationStatus.Completed,
            Description = "Ten years ago, the Gate appeared.",
        };

        foreach (var number in Enumerable.Range(1, 13))
        {
            series.Volumes.Add(new Volume { Id = Guid.CreateVersion7(), Number = number });
        }

        var (run, events) = await RefreshAsync(CreateRefresher(), series);

        series.Volumes.Count.ShouldBe(15);
        series.Volumes.Where(volume => volume.Number <= 13).ShouldAllBe(volume => volume.IsReleased);
        series.Volumes.Where(volume => volume.Number > 13).ShouldAllBe(volume => !volume.IsReleased && volume.ReleaseDate != null);
        run.NewVolumes.ShouldBe(2);
        events.ShouldBeEmpty();
    }

    private static async Task<(SyncRun Run, IReadOnlyList<ShelfEvent> Events)> RefreshAsync(
        SeriesRefresher refresher,
        Series series)
    {
        var run = new SyncRun { Id = Guid.CreateVersion7(), StartedAt = Now };

        var events = await refresher.RefreshAsync(series, run, [], new EnrichmentBudget(8, 8), CancellationToken.None);

        return (run, events);
    }

    private static SeriesRefresher CreateRefresher()
    {
        var info = MangaUpdatesSeriesMapper.ToSeriesInfo(
            MangaUpdatesFixtures.LoadSeries(MangaUpdatesFixtures.SoloLevelingSeries));

        var releaseSource = Substitute.For<IReleaseSource>();
        releaseSource.SourceName.Returns("MangaUpdates");
        releaseSource.GetSeriesAsync(MangaUpdatesFixtures.SoloLevelingSeriesId, Arg.Any<CancellationToken>())
            .Returns(info);

        return new SeriesRefresher(
            Substitute.For<IReleaseSourceMatcher>(),
            releaseSource,
            new SeriesDescriptionHealer(Substitute.For<ISeriesMetadataSource>(), NullLogger<SeriesDescriptionHealer>.Instance),
            new ReleaseCountWriter(releaseSource, NullLogger<ReleaseCountWriter>.Instance),
            new PublicationStatusWriter(releaseSource, NullLogger<PublicationStatusWriter>.Instance),
            VolumeEnrichmentPipeline.Create(
                Catalogue([.. AppleBooksFixtures.LoadCatalogue(AppleBooksFixtures.SoloLevelingSearch)]),
                NoEnrichment(),
                NoCandidates(),
                VolumeEnrichmentPipeline.Writer(CoverCache())),
            ChapterMapPipeline.NoSources(),
            new FixedTimeProvider(Now));
    }

    /// <summary>
    /// The owner's series as the nightly sweep left it: thirteen volumes in print by the count,
    /// already described by Open Library, one of them owned, swept yesterday.
    /// </summary>
    private static Series OwnersShelf()
    {
        var series = new Series
        {
            Id = Guid.CreateVersion7(),
            AniListId = 105398,
            MangaUpdatesId = MangaUpdatesFixtures.SoloLevelingSeriesId,
            TitleEnglish = "Solo Leveling",
            Format = Format.Manga,
            PublicationStatus = PublicationStatus.Completed,
            Description = "Ten years ago, the Gate appeared.",
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
                ReleaseDate = new DateOnly(2021, 3, 2).AddMonths(number * 4),
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
                volume.IsLocalOnly)),
    ];

    private sealed record VolumeFactsSnapshot(
        decimal Number,
        bool IsReleased,
        DateOnly? ReleaseDate,
        MetadataSourceName? ReleaseDateSource,
        string? Title,
        string? Description,
        string? CoverObjectKey,
        bool IsLocalOnly);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
