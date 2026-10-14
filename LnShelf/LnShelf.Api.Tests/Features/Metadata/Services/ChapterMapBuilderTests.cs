using LnShelf.Api.Tests.Features.Metadata.Fixtures;

namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// Raw per-volume chapter lists into ranges, against the captured aggregates: strays dropped,
/// ends known only where the next start is, and maps that do not fit the shelf refused.
/// </summary>
public sealed class ChapterMapBuilderTests
{
    // Chainsaw Man files ch 232 under vol 1 and ch 203 under vol 20. Neither may stretch its
    // volume: vol 1 ends where vol 2 starts, and vol 20 stays open because vols 21–23 are missing.
    [Fact]
    public void Build_ChainsawMan_DropsStraysAndLeavesTheVolumeBeforeAGapOpen()
    {
        var map = ChapterMapBuilder.Build(ChainsawMan(), Shelf(tracked: 22, releasedThrough: 20, latest: 232m));

        map.IsUsable.ShouldBeTrue(map.RejectedBecause);
        Range(map, 1).ShouldBe(new VolumeChapterRange(1, 1m, 7m));
        Range(map, 2).ShouldBe(new VolumeChapterRange(2, 8m, 16.5m));
        Range(map, 19).ShouldBe(new VolumeChapterRange(19, 165m, 175.5m));
        Range(map, 20).ShouldBe(new VolumeChapterRange(20, 176m, null));
        Range(map, 24).ShouldBe(new VolumeChapterRange(24, 230m, null));
        map.Ranges.Any(range => range.VolumeNumber is 21 or 22 or 23).ShouldBeFalse();
    }

    // Frieren's English aggregate is clean: every volume closes on the next one's start, and the
    // newest stays open while the series runs.
    [Fact]
    public void Build_Frieren_EveryVolumeClosesOnTheNextStart()
    {
        var map = ChapterMapBuilder.Build(Frieren(), Shelf(tracked: 14, releasedThrough: 14, latest: 140m));

        map.IsUsable.ShouldBeTrue(map.RejectedBecause);
        map.Ranges.Count.ShouldBe(14);
        Range(map, 1).ShouldBe(new VolumeChapterRange(1, 1m, 7m));
        Range(map, 4).ShouldBe(new VolumeChapterRange(4, 28m, 37m));
        Range(map, 13).ShouldBe(new VolumeChapterRange(13, 118m, 127m));
        Range(map, 14).ShouldBe(new VolumeChapterRange(14, 128m, null));
    }

    [Fact]
    public void Build_CompletedSeries_ClosesItsHighestVolumeOnItsLastChapter()
    {
        var map = ChapterMapBuilder.Build(Frieren(), Shelf(tracked: 14, releasedThrough: 14, latest: 140m, completed: true));

        Range(map, 14).ShouldBe(new VolumeChapterRange(14, 128m, 137m));
    }

    [Fact]
    public void Build_SlimeStrayUnderVolumeFour_IsDropped()
    {
        var chapters = MangaDexFixtures.ChaptersByVolume(MangaDexFixtures.SlimeEnglish, MangaDexFixtures.SlimeAll);

        var map = ChapterMapBuilder.Build(chapters, Shelf(tracked: 31, releasedThrough: 31, latest: 146m));

        map.IsUsable.ShouldBeTrue(map.RejectedBecause);
        Range(map, 4).ShouldBe(new VolumeChapterRange(4, 18m, 22m));
    }

    // Solo Leveling's "volumes" are a Korean season's 110 chapters; the shelf's English
    // edition has thirteen volumes for 200 chapters.
    [Fact]
    public void Build_VolumesFarFatterThanTheShelfsPace_IsRefused()
    {
        var chapters = MangaDexFixtures.ChaptersByVolume(
            MangaDexFixtures.SoloLevelingEnglish, MangaDexFixtures.SoloLevelingAll);

        var map = ChapterMapBuilder.Build(chapters, Shelf(tracked: 13, releasedThrough: 13, latest: 200m));

        map.IsUsable.ShouldBeFalse();
        map.RejectedBecause.ShouldNotBeNull().ShouldContain("average");
        map.Ranges.ShouldBeEmpty();
    }

    public static TheoryData<string, Dictionary<int, IReadOnlyList<decimal>>, int, int, string> RefusalCases => new()
    {
        // name, chapters by volume, tracked, released through, words in the reason
        { "overlapping volumes", new() { [1] = [5m, 6m, 7m], [2] = [1m, 2m, 3m] }, 2, 2, "overlap" },
        { "more volumes than the shelf", new() { [1] = [1m], [2] = [2m], [3] = [3m], [4] = [4m] }, 2, 2, "tracks 2" },
        { "too few released volumes", new() { [1] = [1m, 2m, 3m] }, 4, 4, "1 of the 4" },
        { "nothing filed under a volume", new() { [1] = [] }, 2, 2, "no chapters" },
    };

    [Theory]
    [MemberData(nameof(RefusalCases))]
    public void Build_AMapThatDoesNotFit_IsRefusedWithAReason(
        string name,
        Dictionary<int, IReadOnlyList<decimal>> chapters,
        int tracked,
        int releasedThrough,
        string expected)
    {
        var map = ChapterMapBuilder.Build(chapters, Shelf(tracked, releasedThrough, latest: null));

        map.IsUsable.ShouldBeFalse(name);
        map.RejectedBecause.ShouldNotBeNull().ShouldContain(expected);
    }

    // Where a volume ends depends on where the missing one starts — even for a completed series.
    [Fact]
    public void Build_GapAfterAVolume_LeavesItOpen()
    {
        var chapters = new Dictionary<int, IReadOnlyList<decimal>> { [1] = [1m, 2m, 3m], [3] = [7m, 8m, 9m] };

        var map = ChapterMapBuilder.Build(chapters, Shelf(tracked: 3, releasedThrough: 3, latest: 9m, completed: true));

        Range(map, 1).ShouldBe(new VolumeChapterRange(1, 1m, null));
        Range(map, 3).ShouldBe(new VolumeChapterRange(3, 7m, 9m));
    }

    private static IReadOnlyDictionary<int, IReadOnlyList<decimal>> ChainsawMan() =>
        MangaDexFixtures.ChaptersByVolume(MangaDexFixtures.ChainsawManEnglish, MangaDexFixtures.ChainsawManAll);

    private static IReadOnlyDictionary<int, IReadOnlyList<decimal>> Frieren() =>
        MangaDexFixtures.ChaptersByVolume(MangaDexFixtures.FrierenEnglish, MangaDexFixtures.FrierenAll);

    private static VolumeChapterRange Range(ChapterMap map, int volumeNumber) =>
        map.Ranges.Single(range => range.VolumeNumber == volumeNumber);

    private static ChapterMapShelf Shelf(int tracked, int releasedThrough, decimal? latest, bool completed = false) => new(
        tracked,
        Enumerable.Range(1, releasedThrough).ToHashSet(),
        latest,
        completed);
}
