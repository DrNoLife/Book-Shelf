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
