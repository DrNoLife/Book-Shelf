using System.Net;
using LnShelf.Api.Tests.Features.Metadata.Fixtures;

namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// A whole sync through the app's real wiring, asserting the chapter ranges the series page
/// would show afterwards (owner rule, 2026-10-13: sync fixes get a test through real DI).
/// </summary>
// What is real here: the registration of IChapterMapSource, MangaDexClient behind its
// politeness handler, the matcher, the aggregate mapper, the builder, the writer and the
// refresher's place in SeriesRefresher. Only the bytes on the wire are captures.
public sealed class SyncPipelineChapterMapTests
{
    [Fact]
    public async Task RunAsync_AChaptersManga_GetsChapterRangesOnItsVolumes()
    {
        await using var host = new SyncPipelineHost(
            AppleBooksFixtures.ChainsawManSearch,
            MangaUpdatesFixtures.ChainsawManSeries,
            new MangaDexCaptures(
                MangaDexFixtures.ChainsawManSearch, MangaDexFixtures.ChainsawManEnglish, MangaDexFixtures.ChainsawManAll));
        var series = Manga("Chainsaw Man", MangaDexFixtures.ChainsawManAniListId, MangaUpdatesFixtures.ChainsawManSeriesId, volumeCount: 20);
        await host.SeedAsync(series);

        var run = await host.SyncAsync();

        var saved = await host.LoadAsync(series.Id);
        run.Failures.ShouldBe(0);
        host.UnexpectedRequests.ShouldBeEmpty();
        saved.MangaDexId.ShouldBe(MangaDexFixtures.ChainsawManId);
        saved.ChapterMapStatus.ShouldBe(ChapterMapStatus.Mapped);

        var detail = SeriesDetailProjection.ToDetail(saved, DateOnly.FromDateTime(DateTime.UtcNow));
        Shown(detail, 1).ShouldBe((1m, 7m));
        Shown(detail, 2).ShouldBe((8m, 16.5m));
        Shown(detail, 20).ShouldBe((176m, null));
        detail.ChapterRangeSources.ShouldBe([MetadataSourceName.MangaDex]);
        run.ChapterRangesWritten.ShouldBeGreaterThanOrEqualTo(20);

        // Metadata only: no volume's read state moved (CLAUDE.md, sync never owns user state).
        detail.Volumes.ShouldAllBe(volume => volume.ReadStatus == ReadStatus.Unread);
    }

    // Solo Leveling is Korean: its MangaDex "volumes" are seasons. Matched, refused, never
    // remembered, and no aggregate is even asked for.
    [Fact]
    public async Task RunAsync_AManhwa_GetsNoChapterRanges()
    {
        await using var host = new SyncPipelineHost(
            AppleBooksFixtures.SoloLevelingSearch,
            MangaUpdatesFixtures.SoloLevelingSeries,
            new MangaDexCaptures(
                MangaDexFixtures.SoloLevelingSearch, MangaDexFixtures.SoloLevelingEnglish, MangaDexFixtures.SoloLevelingAll));
        var series = Manga("Solo Leveling", 105398, MangaUpdatesFixtures.SoloLevelingSeriesId, volumeCount: 13);
        await host.SeedAsync(series);

        var run = await host.SyncAsync();

        var saved = await host.LoadAsync(series.Id);
        run.Failures.ShouldBe(0);
        saved.ChapterMapStatus.ShouldBe(ChapterMapStatus.Rejected);
        saved.MangaDexId.ShouldBeNull();
        saved.Volumes.ShouldAllBe(volume => volume.FirstChapter == null && volume.ChapterRangeSource == null);
        host.MangaDexRequests.ShouldNotContain(uri => uri.AbsolutePath.EndsWith("/aggregate"));
    }

    // Wikipedia ahead of MangaDex through the real wiring: found by the template search (Chainsaw
    // Man's Wikidata item names no list), verified by the shelf's vol 1 ISBN, remembered, and
    // ahead of MangaDex on every volume — vols 21–23, which MangaDex lacks, included. Where the
    // two start vol 24 seven chapters apart, the run records it.
    [Fact]
    public async Task RunAsync_AMangaWithAWikipediaList_GetsItsRangesAndRecordsWhereMangaDexDisagrees()
    {
        await using var host = new SyncPipelineHost(
            AppleBooksFixtures.ChainsawManSearch,
            MangaUpdatesFixtures.ChainsawManSeries,
            new MangaDexCaptures(
                MangaDexFixtures.ChainsawManSearch, MangaDexFixtures.ChainsawManEnglish, MangaDexFixtures.ChainsawManAll),
            new WikimediaCaptures(
                ItemSearch: WikipediaFixtures.ChainsawManItemSearch,
                Claims: WikipediaFixtures.ChainsawManClaims,
                PageSearch: WikipediaFixtures.ChainsawManPageSearch,
                Pages: new Dictionary<string, string> { [WikipediaFixtures.ChainsawManPageTitle] = WikipediaFixtures.ChainsawManPage }));
        var series = Manga("Chainsaw Man", MangaDexFixtures.ChainsawManAniListId, MangaUpdatesFixtures.ChainsawManSeriesId, volumeCount: 24);
        series.Volumes.Single(volume => volume.Number == 1).Isbn = WikipediaFixtures.ChainsawManVolumeOneIsbn;
        await host.SeedAsync(series);

        var run = await host.SyncAsync();

        var saved = await host.LoadAsync(series.Id);
        run.Failures.ShouldBe(0);
        host.UnexpectedRequests.ShouldBeEmpty();
        saved.ChapterListPage.ShouldBe(WikipediaFixtures.ChainsawManPageTitle);
        saved.ChapterMapStatus.ShouldBe(ChapterMapStatus.Mapped);

        var detail = SeriesDetailProjection.ToDetail(saved, DateOnly.FromDateTime(DateTime.UtcNow));
        Shown(detail, 1).ShouldBe((1m, 7m));
        Shown(detail, 20).ShouldBe((176m, 186m));
        Shown(detail, 21).ShouldBe((187m, 198m));
        Shown(detail, 24).First.ShouldBe(223m);
        detail.ChapterRangeSources.ShouldBe([MetadataSourceName.Wikipedia]);
        detail.ChapterListPage.ShouldBe(WikipediaFixtures.ChainsawManPageTitle);

        var disagreement = run.Discrepancies.Where(discrepancy => discrepancy.Field == SyncDiscrepancyField.ChapterRange).ShouldHaveSingleItem();
        disagreement.VolumeNumber.ShouldBe(24m);
        disagreement.ResolvedWith.ShouldBe(MetadataSourceName.Wikipedia);

        // The series names the run that checked it, and the page reads the disagreement from it.
        saved.ChapterMapSyncRunId.ShouldBe(run.Id);
        var conflict = (await host.ConflictsAsync(series.Id))
            .Where(candidate => candidate.Field == SyncDiscrepancyField.ChapterRange)
            .ShouldHaveSingleItem();
        conflict.VolumeNumber.ShouldBe(24m);
        conflict.DisplayedSource.ShouldBe(MetadataSourceName.Wikipedia);
    }

    // The remembered MangaDex record is gone (its aggregate answers 404). Wikipedia's answer is
    // still written, the check is stamped so the dead link is not retried every night, and the
    // link is forgotten so the next due check matches the series afresh.
    [Fact]
    public async Task RunAsync_RememberedMangaDexRecordIsGone_WritesWikipediasMapAndForgetsTheLink()
    {
        await using var host = new SyncPipelineHost(
            AppleBooksFixtures.ChainsawManSearch,
            MangaUpdatesFixtures.ChainsawManSeries,
            new MangaDexCaptures(MangaDexFixtures.ChainsawManSearch, AggregateStatus: HttpStatusCode.NotFound),
            ChainsawManWikimedia());
        var series = Manga("Chainsaw Man", MangaDexFixtures.ChainsawManAniListId, MangaUpdatesFixtures.ChainsawManSeriesId, volumeCount: 24);
        series.Volumes.Single(volume => volume.Number == 1).Isbn = WikipediaFixtures.ChainsawManVolumeOneIsbn;
        series.MangaDexId = MangaDexFixtures.ChainsawManId;
        await host.SeedAsync(series);

        var run = await host.SyncAsync();

        var saved = await host.LoadAsync(series.Id);
        run.Failures.ShouldBe(0);
        saved.MangaDexId.ShouldBeNull();
        saved.ChapterMapStatus.ShouldBe(ChapterMapStatus.Mapped);
        saved.ChapterMapCheckedAt.ShouldNotBeNull();
        saved.ChapterMapSyncRunId.ShouldBe(run.Id);
        host.MangaDexRequests.ShouldAllBe(uri => uri.AbsolutePath.EndsWith("/aggregate"));

        var detail = SeriesDetailProjection.ToDetail(saved, DateOnly.FromDateTime(DateTime.UtcNow));
        Shown(detail, 1).ShouldBe((1m, 7m));
        detail.ChapterRangeSources.ShouldBe([MetadataSourceName.Wikipedia]);
    }

    // MangaDex's allowance for the run cannot cover a series, so the series is left whole for
    // the next run — and Wikipedia, asked first in precedence, is not asked either.
    [Fact]
    public async Task RunAsync_MangaDexAllowanceTooSmallForASeries_AsksWikipediaNothing()
    {
        await using var host = new SyncPipelineHost(
            AppleBooksFixtures.ChainsawManSearch,
            MangaUpdatesFixtures.ChainsawManSeries,
            new MangaDexCaptures(
                MangaDexFixtures.ChainsawManSearch, MangaDexFixtures.ChainsawManEnglish, MangaDexFixtures.ChainsawManAll),
            ChainsawManWikimedia(),
            new Dictionary<string, string?> { ["MangaDex:MaximumRequestsPerRun"] = "3" });
        var series = Manga("Chainsaw Man", MangaDexFixtures.ChainsawManAniListId, MangaUpdatesFixtures.ChainsawManSeriesId, volumeCount: 24);
        series.Volumes.Single(volume => volume.Number == 1).Isbn = WikipediaFixtures.ChainsawManVolumeOneIsbn;
        await host.SeedAsync(series);

        var run = await host.SyncAsync();

        var saved = await host.LoadAsync(series.Id);
        run.Failures.ShouldBe(0);
        host.WikimediaRequests.ShouldBeEmpty();
        host.MangaDexRequests.ShouldBeEmpty();
        saved.ChapterMapCheckedAt.ShouldBeNull();
        saved.ChapterListPage.ShouldBeNull();
        saved.Volumes.ShouldAllBe(volume => volume.FirstChapter == null);
    }

    // A completed series whose shelf tracks two volumes past MangaDex's last mapped one: that
    // volume is not the final one, so it must not be closed on MangaDex's last listed chapter.
    [Fact]
    public async Task RunAsync_ACompletedSeriesTheMapStopsShortOf_LeavesTheLastMappedVolumeOpen()
    {
        await using var host = new SyncPipelineHost(
            AppleBooksFixtures.ChainsawManSearch,
            MangaUpdatesFixtures.ChainsawManSeries,
            new MangaDexCaptures(
                MangaDexFixtures.ChainsawManSearch, MangaDexFixtures.ChainsawManEnglish, MangaDexFixtures.ChainsawManAll));
        var series = Manga("Chainsaw Man", MangaDexFixtures.ChainsawManAniListId, MangaUpdatesFixtures.ChainsawManSeriesId, volumeCount: 26);
        series.PublicationStatus = PublicationStatus.Completed;
        await host.SeedAsync(series);

        var run = await host.SyncAsync();

        var saved = await host.LoadAsync(series.Id);
        run.Failures.ShouldBe(0);
        saved.PublicationStatus.ShouldBe(PublicationStatus.Completed);
        saved.ChapterMapStatus.ShouldBe(ChapterMapStatus.Mapped);

        var detail = SeriesDetailProjection.ToDetail(saved, DateOnly.FromDateTime(DateTime.UtcNow));
        Shown(detail, 24).ShouldBe((230m, null));
    }

    private static WikimediaCaptures ChainsawManWikimedia() => new(
        ItemSearch: WikipediaFixtures.ChainsawManItemSearch,
        Claims: WikipediaFixtures.ChainsawManClaims,
        PageSearch: WikipediaFixtures.ChainsawManPageSearch,
        Pages: new Dictionary<string, string> { [WikipediaFixtures.ChainsawManPageTitle] = WikipediaFixtures.ChainsawManPage });

    // Each volume credited to the source that filled it. Berserk's list numbers vols 5–43 and
    // lists 1–4 by title only; MangaDex numbers those four's prologue chapters 0.01–0.14 and
    // fills them. Its all-languages aggregate counts the prologue as 1–16 as well, which must
    // not leak into the English breaks (MangaDexAggregateMapper).
    [Fact]
    public async Task RunAsync_WikipediaLeavesVolumesUnnumbered_MangaDexFillsThemCreditedPerVolume()
    {
        // The store answers with Chainsaw Man's catalogue, which the title arbiter refuses: this
        // test is not about the store, and no Berserk capture of it exists.
        await using var host = new SyncPipelineHost(
            AppleBooksFixtures.ChainsawManSearch,
            MangaUpdatesFixtures.BerserkSeries,
            new MangaDexCaptures(MangaDexFixtures.BerserkSearch, MangaDexFixtures.BerserkEnglish, MangaDexFixtures.BerserkAll),
            new WikimediaCaptures(
                ItemSearch: WikipediaFixtures.BerserkItemSearch,
                Claims: WikipediaFixtures.BerserkClaims,
                PageSearch: WikipediaFixtures.BerserkPageSearch,
                Pages: new Dictionary<string, string> { [WikipediaFixtures.BerserkPageTitle] = WikipediaFixtures.BerserkPage }));
        var series = Manga("Berserk", WikipediaFixtures.BerserkAniListId, MangaUpdatesFixtures.BerserkSeriesId, volumeCount: 43);
        series.Volumes.Single(volume => volume.Number == 1).Isbn = WikipediaFixtures.BerserkVolumeOneIsbn;
        await host.SeedAsync(series);

        var run = await host.SyncAsync();

        var saved = await host.LoadAsync(series.Id);
        run.Failures.ShouldBe(0);
        saved.ChapterListPage.ShouldBe(WikipediaFixtures.BerserkPageTitle);

        var detail = SeriesDetailProjection.ToDetail(saved, DateOnly.FromDateTime(DateTime.UtcNow));
        detail.Volumes.Count(volume => volume.FirstChapter is not null).ShouldBe(43);
        detail.Volumes.Where(volume => volume.Number <= 4).ShouldAllBe(volume => volume.ChapterRangeSource == MetadataSourceName.MangaDex);
        detail.Volumes.Where(volume => volume.Number >= 5).ShouldAllBe(volume => volume.ChapterRangeSource == MetadataSourceName.Wikipedia);
        Shown(detail, 1).ShouldBe((0.01m, 0.03m));
        Shown(detail, 4).ShouldBe((0.10m, 0.14m));
        Shown(detail, 5).ShouldBe((1m, 6m));
        Shown(detail, 42).ShouldBe((365m, 373m));
        detail.ChapterRangeSources.ShouldBe([MetadataSourceName.MangaDex, MetadataSourceName.Wikipedia]);
    }

    private static (decimal? First, decimal? Last) Shown(SeriesDetailResponse detail, decimal number)
    {
        var volume = detail.Volumes.Single(candidate => candidate.Number == number);
        return (volume.FirstChapter, volume.LastChapter);
    }

    private static Series Manga(string title, int aniListId, long mangaUpdatesId, int volumeCount)
    {
        var series = new Series
        {
            Id = Guid.CreateVersion7(),
            AniListId = aniListId,
            MangaUpdatesId = mangaUpdatesId,
            Format = Format.Manga,
            TitleEnglish = title,
            PublicationStatus = PublicationStatus.Ongoing,

            // Set, so the description healer has no reason to ask AniList.
            Description = "A series.",
            LastSyncedAt = DateTimeOffset.UtcNow.AddDays(-1),
        };

        for (var number = 1; number <= volumeCount; number++)
        {
            series.Volumes.Add(new Volume { Id = Guid.CreateVersion7(), Number = number, IsReleased = true });
        }

        return series;
    }
}
