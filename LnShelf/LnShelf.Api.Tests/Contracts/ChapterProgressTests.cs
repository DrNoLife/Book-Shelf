using System.Text.Json;
using LnShelf.Shared.Auth;

namespace LnShelf.Api.Tests.Contracts;

/// <summary>
/// The shared chapter rules: which formats may count in chapters, which numbers can be saved,
/// and the derived "to read" figure.
/// </summary>
public sealed class ChapterProgressTests
{
    [Theory]
    [InlineData(Format.Manga, true)]
    [InlineData(Format.WebNovel, true)]
    [InlineData(Format.LightNovel, false)]
    public void IsOfferedFor_EachFormat_OnlyMangaAndWebNovels(Format format, bool expected) =>
        ChapterProgress.IsOfferedFor(format).ShouldBe(expected);

    [Theory]
    // Text in, because an attribute cannot carry a decimal.
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("142")]
    [InlineData("10.5")]
    [InlineData("10.25")]
    [InlineData("100000")]
    public void ProblemWith_AChapterInRange_HasNone(string chapter) =>
        ChapterProgress.ProblemWith(Parse(chapter)).ShouldBeNull();

    [Theory]
    [InlineData("-1", "negative")]
    [InlineData("-0.5", "negative")]
    [InlineData("100000.01", "at most 100000")]
    [InlineData("10.555", "two decimals")]
    public void ProblemWith_AChapterOutOfRange_SaysWhyInWords(string chapter, string expected) =>
        ChapterProgress.ProblemWith(Parse(chapter)).ShouldNotBeNull().ShouldContain(expected);

    public static TheoryData<decimal?, decimal?, decimal?> ToReadCases => new()
    {
        // latest, last read -> to read
        { 232m, 142m, 90m },
        { 232m, 231.5m, 0.5m },

        // Level with or ahead of a source that lags the simulpub: nothing claimed.
        { 232m, 232m, null },
        { 232m, 240m, null },

        // Either side unknown: no figure at all, never a guess.
        { null, 142m, null },
        { 232m, null, null },
        { null, null, null },
    };

    [Theory]
    [MemberData(nameof(ToReadCases))]
    public void ToRead_TheTwoNumbers_IsTheirDifferenceOnlyWhenPositive(
        decimal? latestChapter, decimal? lastChapterRead, decimal? expected) =>
        ChapterProgress.ToRead(latestChapter, lastChapterRead).ShouldBe(expected);

    [Fact]
    public void SeriesDetailResponse_ChaptersToRead_IsDerivedFromTheTwoFields()
    {
        var detail = new SeriesDetailResponse
        {
            SeriesId = Guid.CreateVersion7(),
            TitleEnglish = "Chainsaw Man",
            Format = Format.Manga,
            PublicationStatus = PublicationStatus.Ongoing,
            TrackingStatus = TrackingStatus.Reading,
            Genres = [],
            HasNewVolume = false,
            Counts = new SeriesCountsResponse
            {
                Read = 0,
                Owned = 0,
                Published = 0,
                ToBuy = 0,
                Backlog = 0,
                Tracked = 0,
                NotOut = 0,
                Dated = 0,
            },
            Volumes = [],
            LastChapterRead = 142m,
            LatestChapter = 232m,
        };

        detail.ChaptersToRead.ShouldBe(90m);
    }

    // A payload from the release before chapters existed carries none of the new fields; it
    // must read as the Volumes series it was.
    [Fact]
    public void Contracts_WithoutTheNewFields_ReadAsVolumes()
    {
        new LibraryCardResponse
        {
            SeriesId = Guid.CreateVersion7(),
            TitleEnglish = "Chainsaw Man",
            Format = Format.Manga,
            PublicationStatus = PublicationStatus.Ongoing,
            TrackingStatus = TrackingStatus.Reading,
            HasNewVolume = false,
            Read = 0,
            Owned = 0,
            Published = 0,
            ToBuy = 0,
            Backlog = 0,
            Tracked = 0,
        }.ProgressUnit.ShouldBe(ProgressUnit.Volumes);

        default(ProgressUnit).ShouldBe(ProgressUnit.Volumes);
        typeof(AccountExportSeries).GetProperty(nameof(AccountExportSeries.ProgressUnit)).ShouldNotBeNull();
    }

    public static TheoryData<decimal, decimal?> VolumeContainingCases => new()
    {
        // chapter -> volume, over Vol 1 = 1–7, Vol 2 = 8–16.5, Vol 3 = 17– (open)
        { 1m, 1m },
        { 7m, 1m },
        { 7.5m, null },
        { 16.5m, 2m },
        { 17m, 3m },
        { 400m, 3m },
        { 0.5m, null },
    };

    [Theory]
    [MemberData(nameof(VolumeContainingCases))]
    public void VolumeContaining_AChapter_IsTheVolumeWhoseRangeHoldsIt(decimal chapter, decimal? expected) =>
        ChapterProgress.VolumeContaining(chapter, RangedVolumes()).ShouldBe(expected);

    public static TheoryData<decimal?, decimal[]> VolumesFinishedByCases => new()
    {
        // last chapter read -> volumes finished and not yet read (Vol 1 is already read)
        { null, [] },
        { 6m, [] },
        { 7m, [] },
        { 16m, [] },
        { 16.5m, [2m] },

        // Vol 3 is open, so no chapter finishes it; the bonus Vol 2.5 is never offered.
        { 500m, [2m] },
    };

    [Theory]
    [MemberData(nameof(VolumesFinishedByCases))]
    public void VolumesFinishedBy_TheLastChapterRead_IsTheUnreadWholeVolumesItHasPassed(
        decimal? lastChapterRead, decimal[] expected) =>
        ChapterProgress.VolumesFinishedBy(lastChapterRead, RangedVolumes())
            .Select(volume => volume.Number)
            .ShouldBe(expected);

    public static TheoryData<string, decimal[], decimal, decimal[]> SkippedVolumeCases => new()
    {
        // name, volumes read, last chapter read -> offered (twelve volumes of ten chapters each)
        { "nothing read", [], 30m, [1m, 2m, 3m] },
        { "Vol 5 skipped, 6–10 read, past 11", [1m, 2m, 3m, 4m, 6m, 7m, 8m, 9m, 10m], 110m, [11m] },
        { "read through 4, past 6", [1m, 2m, 3m, 4m], 60m, [5m, 6m] },
        { "the chapter lags the volumes read", [1m, 2m, 3m, 4m, 5m, 6m, 7m, 8m, 9m, 10m], 50m, [] },
    };

    // Owner decision 2026-10-14: a volume below the highest one read that is still unread was
    // skipped, and a chapter past it must not offer it back.
    [Theory]
    [MemberData(nameof(SkippedVolumeCases))]
    public void VolumesFinishedBy_AVolumeBelowTheHighestRead_IsNeverOffered(
        string name, decimal[] volumesRead, decimal lastChapterRead, decimal[] expected)
    {
        List<VolumeResponse> volumes =
        [
            .. Enumerable.Range(1, 12).Select(number => Volume(
                number,
                volumesRead.Contains(number) ? ReadStatus.Read : ReadStatus.Unread,
                ((number - 1) * 10) + 1,
                number * 10)),
        ];

        ChapterProgress.VolumesFinishedBy(lastChapterRead, volumes)
            .Select(volume => volume.Number)
            .ShouldBe(expected, name);
    }

    [Theory]
    [InlineData(ProgressUnit.Chapters, 1)]
    [InlineData(ProgressUnit.Volumes, 0)]
    public void SeriesDetailResponse_VolumesFinishedByChapter_IsOfferedOnlyOnAChaptersSeries(
        ProgressUnit progressUnit, int expected)
    {
        var detail = Detail(RangedVolumes()) with { ProgressUnit = progressUnit, LastChapterRead = 20m };

        detail.VolumesFinishedByChapter.Count.ShouldBe(expected);
    }

    [Fact]
    public void SeriesDetailResponse_ChapterRangeSources_AreEachSourceOnceAndOnlyWhereARangeIs()
    {
        var detail = Detail(RangedVolumes());

        detail.ChapterRangeSources.ShouldBe([MetadataSourceName.MangaDex]);
        Detail([Volume(1m, ReadStatus.Unread, null, null)]).ChapterRangeSources.ShouldBeEmpty();
    }

    // Computed on both sides from fields that do travel, so the wire carries neither: the offer
    // would otherwise be a second copy of volumes already in Volumes.
    [Fact]
    public void SeriesDetailResponse_Serialized_LeavesTheDerivedListsOutAndRecomputesThem()
    {
        var detail = Detail(RangedVolumes()) with { ProgressUnit = ProgressUnit.Chapters, LastChapterRead = 20m };

        var json = JsonSerializer.Serialize(detail, JsonSerializerOptions.Web);
        var read = JsonSerializer.Deserialize<SeriesDetailResponse>(json, JsonSerializerOptions.Web)!;

        json.ShouldNotContain("volumesFinishedByChapter");
        json.ShouldNotContain("chapterRangeSources");
        read.VolumesFinishedByChapter.Select(volume => volume.Number).ShouldBe([2m]);
        read.ChapterRangeSources.ShouldBe([MetadataSourceName.MangaDex]);
    }

    private static List<VolumeResponse> RangedVolumes() =>
    [
        Volume(1m, ReadStatus.Read, 1m, 7m),
        Volume(2m, ReadStatus.Unread, 8m, 16.5m),
        Volume(2.5m, ReadStatus.Unread, null, null),
        Volume(3m, ReadStatus.Reading, 17m, null),
    ];

    private static VolumeResponse Volume(decimal number, ReadStatus readStatus, decimal? first, decimal? last) => new()
    {
        VolumeId = Guid.CreateVersion7(),
        Number = number,
        IsOut = true,
        ReadStatus = readStatus,
        Owned = false,
        IsLocalOnly = false,
        FirstChapter = first,
        LastChapter = last,
        ChapterRangeSource = first is null ? null : MetadataSourceName.MangaDex,
    };

    private static SeriesDetailResponse Detail(IReadOnlyList<VolumeResponse> volumes) => new()
    {
        SeriesId = Guid.CreateVersion7(),
        TitleEnglish = "Chainsaw Man",
        Format = Format.Manga,
        PublicationStatus = PublicationStatus.Ongoing,
        TrackingStatus = TrackingStatus.Reading,
        Genres = [],
        HasNewVolume = false,
        Counts = new SeriesCountsResponse
        {
            Read = 0,
            Owned = 0,
            Published = 0,
            ToBuy = 0,
            Backlog = 0,
            Tracked = 0,
            NotOut = 0,
            Dated = 0,
        },
        Volumes = volumes,
    };

    private static decimal Parse(string text) => decimal.Parse(text, CultureInfo.InvariantCulture);
}
