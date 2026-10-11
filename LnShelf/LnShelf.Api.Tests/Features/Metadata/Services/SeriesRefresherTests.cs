using static LnShelf.Api.Tests.Features.Metadata.Services.VolumeEnrichmentTestContext;

namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// What a refresh does to one series with the release source's answer in hand: the
/// publication status, who wins a volume-count disagreement, and the new-volume badge.
/// </summary>
// The refresher is built directly rather than through the sync service, the same way the
// description tests build it: what is under test is one series' worth of work.
public sealed class SeriesRefresherTests
{
    private const int AniListId = 94846;
    private const long MangaUpdatesId = 24348008396;

    [Fact]
    public async Task RefreshAsync_TheSourceSaysPublicationHasFinished_MarksTheSeriesCompleted()
    {
        var series = CreateSeries(volumeCount: 3, PublicationStatus.Ongoing);

        var run = await RefreshAsync(series, Info(volumeCount: 3, isCompleted: true));

        // Publication status belongs to MangaUpdates (CLAUDE.md). Before this, the column was
        // written once at add time, so a series that finished publishing afterwards stayed
        // Ongoing for good.
        series.PublicationStatus.ShouldBe(PublicationStatus.Completed);
        run.Discrepancies.ShouldBeEmpty();
    }

    [Fact]
    public async Task RefreshAsync_TheSourceDoesNotSayFinished_LeavesARicherStatusAlone()
    {
        var series = CreateSeries(volumeCount: 3, PublicationStatus.Hiatus);

        var run = await RefreshAsync(series, Info(volumeCount: 3, isCompleted: false));

        // The source's answer is a boolean, and a false cannot tell Ongoing from Hiatus from
        // Cancelled. Writing one would throw away what AniList already knew.
        series.PublicationStatus.ShouldBe(PublicationStatus.Hiatus);
        run.Discrepancies.ShouldBeEmpty();
    }

    [Fact]
    public async Task RefreshAsync_TheShelfSaysCompletedAndTheSourceDoesNot_RecordsItAndWritesNothing()
    {
        var series = CreateSeries(volumeCount: 3, PublicationStatus.Completed);

        var run = await RefreshAsync(series, Info(volumeCount: 3, isCompleted: false, rawStatus: "3 Volumes (Ongoing)"));

        // Recorded rather than resolved: nothing was written, and which source is right is a
        // question a boolean cannot settle.
        series.PublicationStatus.ShouldBe(PublicationStatus.Completed);

        var discrepancy = run.Discrepancies.ShouldHaveSingleItem();
        discrepancy.Field.ShouldBe(SyncDiscrepancyField.Status);
        discrepancy.ResolvedWith.ShouldBeNull();
        discrepancy.Note.ShouldNotBeNull().ShouldContain("3 Volumes (Ongoing)");

        // One (source, value) pair per disagreeing source, which is the shape that lets a
        // third source have an opinion later.
        discrepancy.Values.Single(value => value.Source is MetadataSourceName.AniList)
            .Value.ShouldBe("Completed");
        discrepancy.Values.Single(value => value.Source is MetadataSourceName.MangaUpdates)
            .Value.ShouldBe("Ongoing");
    }

    [Fact]
    public async Task RefreshAsync_BothAgreePublicationHasFinished_RecordsNothing()
    {
        var series = CreateSeries(volumeCount: 3, PublicationStatus.Completed);

        var run = await RefreshAsync(series, Info(volumeCount: 3, isCompleted: true));

        // Agreement is not a discrepancy, and re-running a sync must add nothing
        // (testing.md #3 — idempotency).
        series.PublicationStatus.ShouldBe(PublicationStatus.Completed);
        run.Discrepancies.ShouldBeEmpty();
    }

    [Fact]
    public async Task RefreshAsync_TheSourceCountsMoreVolumes_ResolvesTheRowInTheSourcesFavour()
    {
        // The real 86 case: the shelf tracks 11, MangaUpdates counts 14.
        var series = CreateSeries(volumeCount: 11, PublicationStatus.Ongoing);

        var run = await RefreshAsync(series, Info(volumeCount: 14, isCompleted: false));

        var discrepancy = run.Discrepancies.ShouldHaveSingleItem();
        discrepancy.Field.ShouldBe(SyncDiscrepancyField.VolumeCount);

        // The row used to say AniList in both directions, which was a lie in exactly this
        // one: the three missing volumes are created, so MangaUpdates' count is what won.
        discrepancy.ResolvedWith.ShouldBe(MetadataSourceName.MangaUpdates);
        series.Volumes.Count.ShouldBe(14);
        run.NewVolumes.ShouldBe(3);
    }

    // An import creates only the volumes in the export, so a shelf can hold 2, 3 and 5 of a
    // ten-volume series. Sync used to count up from the highest tracked number and never
    // fill 1 or 4 (audit 2026-10-09).
    [Fact]
    public async Task RefreshAsync_TheShelfHasGapsBelowItsHighestVolume_CreatesTheMissingNumbers()
    {
        var series = CreateSeries(volumeCount: 0, PublicationStatus.Ongoing);
        foreach (var number in new[] { 2, 3, 5 })
        {
            series.Volumes.Add(new Volume { Id = Guid.CreateVersion7(), Number = number });
        }

        var run = await RefreshAsync(series, Info(volumeCount: 10, isCompleted: false));

        series.Volumes.Select(volume => (int)volume.Number).Order().ShouldBe(Enumerable.Range(1, 10));
        run.NewVolumes.ShouldBe(7);
    }

    [Fact]
    public async Task RefreshAsync_ABonusVolumeOnTopOfTheCount_IsNeitherADiscrepancyNorLeftUnreleased()
    {
        // Torture Princess: MangaUpdates says "9 Volumes + 1 Extra", which reads as nine, and
        // the shelf tracks 1–9 plus the bonus Vol. 7.5. That is agreement, not a tenth volume.
        var series = CreateSeries(volumeCount: 9, PublicationStatus.Completed);
        series.Volumes.Add(new Volume { Id = Guid.CreateVersion7(), Number = 7.5m });

        var run = await RefreshAsync(series, Info(volumeCount: 9, isCompleted: true));

        run.Discrepancies.ShouldBeEmpty();
        run.NewVolumes.ShouldBe(0);
        series.Volumes.Single(volume => volume.Number == 7.5m).IsReleased.ShouldBeTrue();
    }

    [Fact]
    public async Task RefreshAsync_TheSourceCountsFewerVolumes_ResolvesTheRowInTheShelfsFavour()
    {
        // The real Overlord case: AniList tracks 18, MangaUpdates counts 16.
        var series = CreateSeries(volumeCount: 18, PublicationStatus.Ongoing);

        var run = await RefreshAsync(series, Info(volumeCount: 16, isCompleted: false));

        var discrepancy = run.Discrepancies.ShouldHaveSingleItem();

        // Nothing is deleted, so the shelf's own list stands and this half of the branch
        // genuinely is AniList-resolved.
        discrepancy.ResolvedWith.ShouldBe(MetadataSourceName.AniList);
        series.Volumes.Count.ShouldBe(18);
    }

    [Fact]
    public async Task RefreshAsync_ASeriesFirstEverRefresh_DoesNotRaiseTheNewVolumeBadge()
    {
        // What runs the moment a series is added: LastSyncedAt is still null.
        var series = CreateSeries(volumeCount: 3, PublicationStatus.Ongoing, withUserState: true);

        var run = await RefreshAsync(series, Info(volumeCount: 3, isCompleted: false));

        // Three volumes just went from "unknown" to "in print", and none of them is news —
        // the reader added the series knowing what was on the shelf.
        run.VolumesMarkedReleased.ShouldBe(3);
        series.UserState.ShouldNotBeNull().HasNewVolume.ShouldBeFalse();
    }

    [Fact]
    public async Task RefreshAsync_AVolumeGoesOnSaleAfterAnEarlierRefresh_RaisesTheNewVolumeBadge()
    {
        var series = CreateSeries(volumeCount: 3, PublicationStatus.Ongoing, withUserState: true);
        MarkSweptWithEverythingInPrint(series);

        var run = await RefreshAsync(series, Info(volumeCount: 4, isCompleted: false));

        // Volume 4 was created by this refresh and is inside the source's count, so it is out.
        run.VolumesMarkedReleased.ShouldBe(1);
        series.UserState.ShouldNotBeNull().HasNewVolume.ShouldBeTrue();
    }

    [Fact]
    public async Task RefreshAsync_AVolumeTheShelfAlreadyTrackedGoesOnSale_RaisesTheNewVolumeBadge()
    {
        var series = CreateSeries(volumeCount: 4, PublicationStatus.Ongoing, withUserState: true);
        MarkSweptWithEverythingInPrint(series);

        // The announced-but-unreleased case: AniList listed volume 4 before it existed in
        // print, and now the count reaches it.
        series.Volumes.Single(volume => volume.Number == 4).IsReleased = false;

        await RefreshAsync(series, Info(volumeCount: 4, isCompleted: false));

        series.UserState.ShouldNotBeNull().HasNewVolume.ShouldBeTrue();
    }

    [Fact]
    public async Task RefreshAsync_ARefreshThatFindsNothingNew_LeavesTheBadgeAlone()
    {
        var series = CreateSeries(volumeCount: 3, PublicationStatus.Ongoing, withUserState: true);
        MarkSweptWithEverythingInPrint(series);

        var run = await RefreshAsync(series, Info(volumeCount: 3, isCompleted: false));

        run.VolumesMarkedReleased.ShouldBe(0);
        series.UserState.ShouldNotBeNull().HasNewVolume.ShouldBeFalse();
    }

    [Fact]
    public async Task RefreshAsync_ABadgeTheReaderHasNotSeenYet_IsNeverClearedBySync()
    {
        var series = CreateSeries(volumeCount: 3, PublicationStatus.Ongoing, withUserState: true);
        MarkSweptWithEverythingInPrint(series);
        series.UserState!.HasNewVolume = true;

        await RefreshAsync(series, Info(volumeCount: 3, isCompleted: false));

        // Clearing it is the reader's act — the Shelf slice owns that — and a nightly sweep
        // must not dismiss a badge nobody has looked at.
        series.UserState.HasNewVolume.ShouldBeTrue();
    }

    [Fact]
    public async Task RefreshAsync_RaisingTheBadge_TouchesNothingElseOnTheUserState()
    {
        var series = CreateSeries(volumeCount: 3, PublicationStatus.Ongoing, withUserState: true);
        MarkSweptWithEverythingInPrint(series);
        series.UserState!.Notes = "Waiting on the omnibus.";

        await RefreshAsync(series, Info(volumeCount: 4, isCompleted: false));

        // HasNewVolume is the single sanctioned exception to "sync never writes user state";
        // everything beside it on the row is still the reader's.
        series.UserState.HasNewVolume.ShouldBeTrue();
        series.UserState.TrackingStatus.ShouldBe(TrackingStatus.Reading);
        series.UserState.Notes.ShouldBe("Waiting on the omnibus.");
        series.UserState.LastReadAt.ShouldBeNull();
    }

    [Fact]
    public async Task RefreshAsync_ASeriesWithNoUserStateRow_StillRefreshes()
    {
        var series = CreateSeries(volumeCount: 3, PublicationStatus.Ongoing);
        MarkSweptWithEverythingInPrint(series);

        var run = await RefreshAsync(series, Info(volumeCount: 4, isCompleted: false));

        // Creating the row is the Shelf slice's job, and a series nobody tracks has nobody
        // to notify — so the badge is skipped rather than the refresh failing.
        series.UserState.ShouldBeNull();
        run.NewVolumes.ShouldBe(1);
    }

    private static async Task<SyncRun> RefreshAsync(Series series, ReleaseSeriesInfo info)
    {
        var run = new SyncRun { Id = Guid.CreateVersion7(), StartedAt = DateTimeOffset.UnixEpoch };

        await CreateRefresher(info).RefreshAsync(
            series, run, [], new LookupBudget(8), CancellationToken.None);

        return run;
    }

    /// <summary>
    /// A refresher whose release source answers <paramref name="info"/> either way — by
    /// matching an unlinked series or by re-fetching a linked one.
    /// </summary>
    private static SeriesRefresher CreateRefresher(ReleaseSeriesInfo info)
    {
        var matcher = Substitute.For<IReleaseSourceMatcher>();
        matcher.MatchAsync(Arg.Any<Series>(), Arg.Any<IReadOnlySet<long>>(), Arg.Any<CancellationToken>())
            .Returns(new ReleaseSourceMatch
            {
                Outcome = ReleaseMatchOutcome.Linked,
                MatchedExternalId = MangaUpdatesId,
                Score = 100,
                Explanation = "Linked.",
                MatchedSeries = info,
            });

        var releaseSource = Substitute.For<IReleaseSource>();
        releaseSource.SourceName.Returns("MangaUpdates");
        releaseSource.GetSeriesAsync(MangaUpdatesId, Arg.Any<CancellationToken>()).Returns(info);

        return new SeriesRefresher(
            matcher,
            releaseSource,
            NoMetadata(),
            new ReleaseCountWriter(releaseSource, NullLogger<ReleaseCountWriter>.Instance),
            new PublicationStatusWriter(releaseSource, NullLogger<PublicationStatusWriter>.Instance),
            VolumeEnrichmentPipeline.Create(
                NoCatalogue(),
                NoEnrichment(),
                NoCandidates(),
                VolumeEnrichmentPipeline.Writer()),
            TimeProvider.System,
            NullLogger<SeriesRefresher>.Instance);
    }

    private static ReleaseSeriesInfo Info(int volumeCount, bool isCompleted, string? rawStatus = null) => new()
    {
        ExternalId = MangaUpdatesId,
        Title = "Youjo Senki",
        VolumeCount = volumeCount,
        IsCompleted = isCompleted,
        PublicationStatus = isCompleted ? PublicationStatus.Completed : PublicationStatus.Ongoing,
        RawStatus = rawStatus ?? $"{volumeCount} Volumes ({(isCompleted ? "Complete" : "Ongoing")})",
    };

    private static Series CreateSeries(
        int volumeCount,
        PublicationStatus publicationStatus,
        bool withUserState = false)
    {
        var series = new Series
        {
            Id = Guid.CreateVersion7(),
            AniListId = AniListId,
            TitleEnglish = "The Saga of Tanya the Evil",
            Format = Format.LightNovel,
            PublicationStatus = publicationStatus,
            UserState = withUserState
                ? new SeriesUserState { TrackingStatus = TrackingStatus.Reading }
                : null,
        };

        foreach (var number in Enumerable.Range(1, volumeCount))
        {
            series.Volumes.Add(new Volume { Id = Guid.CreateVersion7(), Number = number });
        }

        return series;
    }

    /// <summary>
    /// What a series looks like after an earlier sweep: stamped, linked, everything it
    /// tracks already confirmed in print.
    /// </summary>
    private static void MarkSweptWithEverythingInPrint(Series series)
    {
        series.LastSyncedAt = DateTimeOffset.UnixEpoch;
        series.MangaUpdatesId = MangaUpdatesId;

        foreach (var volume in series.Volumes)
        {
            volume.IsReleased = true;
        }
    }

    /// <summary>AniList knows nothing: the description heal has its own file.</summary>
    private static ISeriesMetadataSource NoMetadata()
    {
        var source = Substitute.For<ISeriesMetadataSource>();
        source.GetSeriesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((SeriesMetadata?)null);

        return source;
    }
}
