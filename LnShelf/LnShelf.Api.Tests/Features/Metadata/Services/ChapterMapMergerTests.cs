using LnShelf.Api.Tests.Features.Metadata.Fixtures;

namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// Usable maps from several sources into one range per volume: precedence, the order checked
/// across sources, ends closed from the merged starts, and disagreements named.
/// </summary>
public sealed class ChapterMapMergerTests
{
    [Fact]
    public void Merge_BothSourcesMapAVolume_TheFirstInPrecedenceWins()
    {
        var wikipedia = Map(new() { [1] = Chapters(1, 7), [2] = Chapters(8, 16) });
        var mangaDex = Map(new() { [1] = Chapters(1, 7), [2] = Chapters(8, 16), [3] = Chapters(17, 25) });

        var merged = Merge((MetadataSourceName.Wikipedia, wikipedia), (MetadataSourceName.MangaDex, mangaDex));

        Range(merged, 1).ShouldBe(new(new VolumeChapterRange(1, 1m, 7m), MetadataSourceName.Wikipedia));
        Range(merged, 2).ShouldBe(new(new VolumeChapterRange(2, 8m, 16m), MetadataSourceName.Wikipedia));
        Range(merged, 3).Source.ShouldBe(MetadataSourceName.MangaDex);
        merged.Disagreements.ShouldBeEmpty();
    }

    // MangaDex lacks Chainsaw Man's vols 21–23, so its vol 20 is open on its own; Wikipedia's
    // vol 21 says where it ends. The end is the whole chapter before that start, even where the
    // open volume's own list stops short of it.
    [Fact]
    public void Merge_OpenVolumeBeforeAnotherSourcesStart_IsClosed()
    {
        var wikipedia = Map(new() { [21] = Chapters(187, 198) });
        var mangaDex = Map(new() { [20] = Chapters(176, 184) });

        var merged = Merge((MetadataSourceName.Wikipedia, wikipedia), (MetadataSourceName.MangaDex, mangaDex));

        Range(merged, 20).ShouldBe(new(new VolumeChapterRange(20, 176m, 186m), MetadataSourceName.MangaDex));
        Range(merged, 21).Range.LastChapter.ShouldBeNull();
    }

    // An extra filed at the end of the volume stays in it.
    [Fact]
    public void Merge_VolumeEndingOnAnExtra_KeepsIt()
    {
        var wikipedia = Map(new() { [3] = Chapters(17, 25) });
        var mangaDex = Map(new() { [2] = [.. Chapters(8, 16), 16.5m] });

        var merged = Merge((MetadataSourceName.Wikipedia, wikipedia), (MetadataSourceName.MangaDex, mangaDex));

        Range(merged, 2).Range.LastChapter.ShouldBe(16.5m);
    }

    // Chainsaw Man's vol 24: Wikipedia starts it at 223, MangaDex at 230. Recorded, and
    // precedence still decides what is shown.
    [Fact]
    public void Merge_StartsMoreThanOneChapterApart_IsADisagreement()
    {
        var wikipedia = Map(new() { [23] = Chapters(211, 222), [24] = Chapters(223, 232) });
        var mangaDex = Map(new() { [24] = [230m] });

        var merged = Merge((MetadataSourceName.Wikipedia, wikipedia), (MetadataSourceName.MangaDex, mangaDex));

        var disagreement = merged.Disagreements.ShouldHaveSingleItem();
        disagreement.VolumeNumber.ShouldBe(24);
        disagreement.WrittenSource.ShouldBe(MetadataSourceName.Wikipedia);
        disagreement.Offers.Select(offer => (offer.Source, offer.Range.FirstChapter))
            .ShouldBe([(MetadataSourceName.Wikipedia, 223m), (MetadataSourceName.MangaDex, 230m)]);
        Range(merged, 24).Range.FirstChapter.ShouldBe(223m);
    }

    // A prologue numbered 0.15 by one source and folded into chapter 1 by the other is style.
    [Fact]
    public void Merge_StartsOneChapterApartOrLess_IsNoDisagreement()
    {
        var wikipedia = Map(new() { [5] = Chapters(1, 6) });
        var mangaDex = Map(new() { [5] = [0.15m, 0.16m, .. Chapters(1, 6)] });

        Merge((MetadataSourceName.Wikipedia, wikipedia), (MetadataSourceName.MangaDex, mangaDex))
            .Disagreements.ShouldBeEmpty();
    }

    // A volume whose start does not rise above the one before it, as merged, gives way to the
    // next source's range for it.
    [Fact]
    public void Merge_VolumeRestartingTheNumbering_FallsBackToTheNextSource()
    {
        var wikipedia = Map(new() { [5] = Chapters(1, 6) });
        var mangaDex = Map(new() { [4] = Chapters(10, 14), [5] = Chapters(15, 22) });

        var merged = Merge((MetadataSourceName.Wikipedia, wikipedia), (MetadataSourceName.MangaDex, mangaDex));

        Range(merged, 5).ShouldBe(new(new VolumeChapterRange(5, 15m, null), MetadataSourceName.MangaDex));
        Range(merged, 4).Range.LastChapter.ShouldBe(14m);
        merged.Disagreements.ShouldHaveSingleItem().WrittenSource.ShouldBe(MetadataSourceName.MangaDex);
    }

    // The real pair: Wikipedia lists Berserk's four prologue volumes without numbers, MangaDex
    // numbers them 0.01–0.14, and from vol 5 both count from chapter 1.
    [Fact]
    public void Merge_Berserk_PrologueVolumesFromMangaDexTheRestFromWikipedia()
    {
        var shelf = new ChapterMapShelf(43, Enumerable.Range(1, 43).ToHashSet(), 386m, IsCompleted: false, HighestTrackedWholeVolume: 43);
        var wikipedia = ChapterMapBuilder.Build(
            GraphicNovelListVerifier.ToChaptersByVolume(WikipediaFixtures.Tables(WikipediaFixtures.BerserkPage)[0]), shelf);
        var mangaDex = ChapterMapBuilder.Build(
            MangaDexFixtures.ChaptersByVolume(MangaDexFixtures.BerserkEnglish, MangaDexFixtures.BerserkAll), shelf);
        wikipedia.IsUsable.ShouldBeTrue(wikipedia.RejectedBecause);
        mangaDex.IsUsable.ShouldBeTrue(mangaDex.RejectedBecause);

        var merged = Merge((MetadataSourceName.Wikipedia, wikipedia), (MetadataSourceName.MangaDex, mangaDex));

        Range(merged, 1).ShouldBe(new(new VolumeChapterRange(1, 0.01m, 0.03m), MetadataSourceName.MangaDex));
        Range(merged, 4).ShouldBe(new(new VolumeChapterRange(4, 0.10m, 0.14m), MetadataSourceName.MangaDex));
        Range(merged, 5).ShouldBe(new(new VolumeChapterRange(5, 1m, 6m), MetadataSourceName.Wikipedia));
        Range(merged, 43).Source.ShouldBe(MetadataSourceName.Wikipedia);
        merged.Ranges.Select(range => range.Range.FirstChapter).ShouldBeInOrder(SortDirection.Ascending);
    }

    // Vol 1 comes from the first source only, at 10; the other two start vol 2 below it, so
    // neither is written. The disagreement must not name a source as shown.
    [Fact]
    public void Merge_NoOfferRisesAboveThePreviousStart_RecordsTheDisagreementWithNoWrittenSource()
    {
        var wikipedia = Map(new() { [1] = Chapters(10, 12) });
        var mangaDex = Map(new() { [2] = Chapters(3, 4) });
        var third = Map(new() { [2] = Chapters(5, 6) });

        var merged = Merge(
            (MetadataSourceName.Wikipedia, wikipedia),
            (MetadataSourceName.MangaDex, mangaDex),
            (MetadataSourceName.AniList, third));

        merged.Ranges.ShouldNotContain(range => range.Range.VolumeNumber == 2);
        var disagreement = merged.Disagreements.ShouldHaveSingleItem();
        disagreement.VolumeNumber.ShouldBe(2);
        disagreement.WrittenSource.ShouldBeNull();
    }

    private static MergedChapterMap Merge(params (MetadataSourceName Source, ChapterMap Map)[] maps) =>
        ChapterMapMerger.Merge(maps);

    private static MergedVolumeRange Range(MergedChapterMap merged, int volumeNumber) =>
        merged.Ranges.Single(range => range.Range.VolumeNumber == volumeNumber);

    // A shelf that refuses nothing, so each test's maps are exactly what it wrote.
    private static ChapterMap Map(Dictionary<int, IReadOnlyList<decimal>> chapters)
    {
        var map = ChapterMapBuilder.Build(chapters, new ChapterMapShelf(100, new HashSet<int>(), null, IsCompleted: false, HighestTrackedWholeVolume: 100));
        map.IsUsable.ShouldBeTrue(map.RejectedBecause);
        return map;
    }

    private static List<decimal> Chapters(int first, int last) =>
        [.. Enumerable.Range(first, last - first + 1).Select(chapter => (decimal)chapter)];
}
