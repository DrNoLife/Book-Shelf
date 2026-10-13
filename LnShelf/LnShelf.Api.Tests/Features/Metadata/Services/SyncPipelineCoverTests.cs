using LnShelf.Api.Tests.Features.Metadata.Fixtures;

namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// One whole sync through the app's real wiring, asserting what the shelf shows afterwards:
/// every volume the store matched has its own cover on disk, its date and its name.
/// </summary>
// The regression this exists for (2026-10-13): a sync logged "matched 14 of 207 records" for
// every series and wrote no cover at all, because 153 volumes carried keys to files the store
// had lost. Every unit test passed, since each one hands its subject doubles; only the sum of
// the parts — the column, the gap rule, the writer, the fetcher and a real folder — had the
// bug. See SyncPipelineHost for what is real here (everything but the wire and Postgres).
//
// Solo Leveling because both of its sources were captured: the store answers Vols. 1 to 15
// and MangaUpdates counts 13 plus 2 extras, so the run also creates two volumes on the way.
public sealed class SyncPipelineCoverTests
{
    private const int MainVolumeCount = 13;
    private const string SeriesCoverKey = "anilist/105398.jpg";

    [Fact]
    public async Task RunAsync_VolumesWithNoCover_EachGetTheirOwnCoverDateAndNameFromOneSync()
    {
        await using var host = Host();
        var series = SoloLeveling(volume => { });
        await host.SeedAsync(series);

        var run = await host.SyncAsync();

        var saved = await host.LoadAsync(series.Id);
        run.Failures.ShouldBe(0);
        host.UnexpectedRequests.ShouldBeEmpty();
        saved.Volumes.Count.ShouldBeGreaterThanOrEqualTo(MainVolumeCount);

        ShouldEachShowTheirOwnCover(host, saved);
        run.VolumeCoversCached.ShouldBe(saved.Volumes.Count);

        // The same loop writes these from the same match; a run that wrote none of them would
        // be the same failure with a different symptom.
        saved.Volumes.ShouldAllBe(volume => volume.ReleaseDate != null);
        saved.Volumes.ShouldAllBe(volume => volume.Title != null);
        saved.Volumes.ShouldAllBe(volume => volume.Description != null);
    }

    [Fact]
    public async Task RunAsync_VolumesWhoseCoverFileIsGone_GetTheirCoverBackFromOneSync()
    {
        await using var host = Host();

        // Complete in every other respect — dated, named, described — so the only thing
        // wrong is the file behind the key. That is the shelf the bug left behind: nothing
        // looked missing, so nothing was ever asked again.
        var series = SoloLeveling(volume =>
        {
            volume.CoverObjectKey = CoverObjectKey.ForVolumeSync(volume.Id, "AppleBooks", ".jpg");
            volume.ReleaseDate = new DateOnly(2021, 3, 2);
            volume.Title = string.Empty;
            volume.Description = "Already described.";
        });
        await host.SeedAsync(series);

        var run = await host.SyncAsync();

        var saved = await host.LoadAsync(series.Id);
        run.Failures.ShouldBe(0);
        ShouldEachShowTheirOwnCover(host, saved);
    }

    [Fact]
    public async Task RunAsync_VolumesWhoseCoverFileIsThere_KeepItWithoutFetchingAgain()
    {
        await using var host = Host();
        var series = SoloLeveling(volume => { });
        await host.SeedAsync(series);
        await host.SyncAsync();
        var fetchedOnFirstRun = host.ImageRequests.Count;

        var run = await host.SyncAsync();

        // The healer only forgets keys whose file is gone. A second night over a healthy
        // shelf must cost no image request and move no key (testing.md #3 — idempotency).
        host.ImageRequests.Count.ShouldBe(fetchedOnFirstRun);
        run.VolumeCoversCached.ShouldBe(0);
        ShouldEachShowTheirOwnCover(host, await host.LoadAsync(series.Id));
    }

    /// <summary>
    /// What a tile shows: the projection's key, which must be the volume's own and on disk.
    /// </summary>
    // Read through the same projection the series page uses, because the visible symptom
    // was its fallback: a volume with no usable key of its own shows the series art.
    private static void ShouldEachShowTheirOwnCover(SyncPipelineHost host, Series saved)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var shown = saved.Volumes
            .Select(volume => SeriesDetailProjection.ToVolume(volume, today, saved.CoverObjectKey).CoverObjectKey)
            .ToList();

        shown.ShouldAllBe(key => key != null && key != SeriesCoverKey);
        shown.Distinct().Count().ShouldBe(saved.Volumes.Count);
        shown.ShouldAllBe(key => host.StoreHolds(key!));
    }

    private static SyncPipelineHost Host() =>
        new(AppleBooksFixtures.SoloLevelingSearch, MangaUpdatesFixtures.SoloLevelingSeries);

    private static Series SoloLeveling(Action<Volume> arrange)
    {
        var series = new Series
        {
            Id = Guid.CreateVersion7(),
            AniListId = 105398,
            MangaUpdatesId = MangaUpdatesFixtures.SoloLevelingSeriesId,
            Format = Format.Manga,
            TitleEnglish = "Solo Leveling",
            PublicationStatus = PublicationStatus.Completed,

            // Set, so the description healer has no reason to ask AniList.
            Description = "A hunter levels up.",
            CoverObjectKey = SeriesCoverKey,
            LastSyncedAt = DateTimeOffset.UtcNow.AddDays(-1),
        };

        for (var number = 1; number <= MainVolumeCount; number++)
        {
            var volume = new Volume { Id = Guid.CreateVersion7(), Number = number, IsReleased = true };
            arrange(volume);
            series.Volumes.Add(volume);
        }

        return series;
    }
}
