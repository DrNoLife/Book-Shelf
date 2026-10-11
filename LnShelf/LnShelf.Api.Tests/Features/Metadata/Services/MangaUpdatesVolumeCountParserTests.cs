namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// Volume counts out of MangaUpdates' free-text status field.
/// </summary>
// Every string here is the real status of a series on the shelf, copied verbatim
// from the API. There is no structured count to read, so this parser is the only
// thing standing between prose and a number the shelf acts on.
public sealed class MangaUpdatesVolumeCountParserTests
{
    [Theory]
    // Overlord and Youjo Senki: the simple case.
    [InlineData("16 Volumes (Ongoing)", 16)]
    [InlineData("14 Volumes (Ongoing)", 14)]

    // Torture Princess: an extra volume that is not part of the count.
    [InlineData("9 Volumes + 1 Extra (Complete)", 9)]

    // 86: side volumes named rather than numbered away.
    [InlineData("14 Volumes + 2 Gaiden Volumes (Ongoing)", 14)]

    // Slime: web-novel and light-novel counts in one field, on separate lines. The
    // light novel is what the shelf tracks, so 23 — not 14, which comes first.
    [InlineData("14 WN Volumes + 2 SS Volumes (Complete)  \n23 LN Volumes + 2 SS Volumes (Complete)", 23)]

    // Mushoku Tensei: same shape, light novel first this time.
    [InlineData("26 LN Volumes (Complete)  \n24 Web Volumes (Complete)", 26)]

    // Nidome no Yuusha: a chapter count for the web novel, volumes only for the
    // light novel.
    [InlineData("168 WN Chapters + 3 Extra (Deleted)  \n8 LN Volumes (Complete)", 8)]

    // Ascendance of a Bookworm: the whole series first, then a per-part breakdown.
    // The leftmost number is the series' own count.
    [InlineData("33 Volumes (Complete) \n\nPart 1: 3 Volumes (Complete)  \nPart 2: 4 Volumes (Complete)", 33)]
    public void Parse_RealStatusText_FindsTheVolumeCount(string status, int expected) =>
        MangaUpdatesVolumeCountParser.Parse(status).ShouldBe(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Ongoing")]
    [InlineData("Complete")]
    [InlineData("168 Chapters (Ongoing)")]
    [InlineData("0 Volumes (Ongoing)")]
    public void Parse_NothingCountable_IsNull(string? status) =>
        MangaUpdatesVolumeCountParser.Parse(status).ShouldBeNull();

    [Fact]
    public void Parse_WebNovelOnly_FallsBackToIt() =>
        // Worse than a light-novel count and better than nothing; the discrepancy row
        // records the status text it came from either way.
        MangaUpdatesVolumeCountParser.Parse("14 WN Volumes (Complete)").ShouldBe(14);

    [Theory]
    // Every one a real English publisher note (captured 2026-10-11). Bonus volumes after a
    // plus sign stay out of the count; the catalogue tracks them as Vol. 7.5 and the like.
    [InlineData("9 Volumes + 1 Extra - Complete", 9)]
    [InlineData("9+2 Volume; Ongoing", 9)]
    [InlineData("11+3 Volumes; Complete", 11)]
    [InlineData("13 Volumes - Ongoing", 13)]
    [InlineData("22 Volumes; Ongoing", 22)]
    [InlineData("29 Volumes - Ongoing", 29)]
    public void ParseEnglishEditions_ARealPublisherNote_ReadsTheMainCount(string notes, int expected) =>
        MangaUpdatesVolumeCountParser.ParseEnglishEditions([notes]).ShouldBe(expected);

    [Theory]
    // Not a count of books this shelf tracks, or one qualified in a way the plain rule should
    // not guess at; the status line answers instead.
    [InlineData("4 Audiobooks; Ongoing")]
    [InlineData("146 Chapters - Ongoing; Digital")]
    [InlineData("33 Digital Volumes; Complete | 26 Physical Volumes; Ongoing")]
    // Six omnibuses: the 1 of "2-in-1" is not a count of one.
    [InlineData("6 2-in-1 Volumes; Ongoing")]
    [InlineData("")]
    [InlineData(null)]
    public void ParseEnglishEditions_NoCountOfBooks_IsNull(string? notes) =>
        MangaUpdatesVolumeCountParser.ParseEnglishEditions([notes]).ShouldBeNull();

    [Theory]
    [InlineData("9 Volumes + 1 Extra - Complete", 1)]
    [InlineData("9+2 Volume; Ongoing", 2)]
    [InlineData("11+3 Volumes; Complete", 3)]
    [InlineData("16 Volumes; Ongoing", 0)]
    [InlineData("4 Audiobooks; Ongoing", 0)]
    [InlineData(null, 0)]
    public void ParseEnglishEditionExtras_ARealPublisherNote_ReadsTheBonusCount(string? notes, int expected) =>
        MangaUpdatesVolumeCountParser.ParseEnglishEditionExtras([notes]).ShouldBe(expected);

    [Fact]
    public void ParseEnglishEditions_TwoEnglishPublishers_TakesTheHigherCount() =>
        // An audiobook imprint beside the print one is the usual pair; a licence that moved
        // between publishers is the other, and the older note stops where it stopped.
        MangaUpdatesVolumeCountParser.ParseEnglishEditions(["14 Audiobooks; Ongoing", "16 Volumes; Ongoing", "12 Volumes; Cancelled"])
            .ShouldBe(16);

    [Fact]
    public void ReadCount_AnEnglishNote_QuotesThePublisherAndItsNote() =>
        MangaUpdatesVolumeCountParser.ReadCount([("Yen On", "9 Volumes + 1 Extra - Complete")], "10 Volumes (Complete)")
            .ShouldBe(new VolumeCountReading(9, VolumeCountBasis.EnglishPublisherNote, "Yen On: 9 Volumes + 1 Extra - Complete"));

    [Fact]
    public void ReadCount_SeveralEnglishNotes_QuotesTheOneWhoseCountWon() =>
        MangaUpdatesVolumeCountParser.ReadCount(
                [("Yen Audio", "14 Audiobooks; Ongoing"), ("Yen On", "16 Volumes; Ongoing"), ("Seven Seas", "12 Volumes; Cancelled")],
                status: null)
            .ShouldBe(new VolumeCountReading(16, VolumeCountBasis.EnglishPublisherNote, "Yen On: 16 Volumes; Ongoing"));

    [Fact]
    public void ReadCount_NoPublisherName_QuotesTheNoteAlone() =>
        MangaUpdatesVolumeCountParser.ReadCount([(null, " 16 Volumes; Ongoing ")], status: null)
            .ShouldBe(new VolumeCountReading(16, VolumeCountBasis.EnglishPublisherNote, "16 Volumes; Ongoing"));

    // Slime: no English note states a count, so the status line does — and the line quoted is
    // the one the count came from, not the web-novel line before it.
    [Fact]
    public void ReadCount_NoEnglishCount_QuotesTheStatusLineTheCountCameFrom() =>
        MangaUpdatesVolumeCountParser.ReadCount(
                [("Yen Audio", "14 Audiobooks; Ongoing")],
                "14 WN Volumes + 2 SS Volumes (Complete)  \n23 LN Volumes + 2 SS Volumes (Complete)")
            .ShouldBe(new VolumeCountReading(23, VolumeCountBasis.MangaUpdatesStatus, "23 LN Volumes + 2 SS Volumes (Complete)"));

    // The count crosses a line break, so no single line carries it: the whole status is quoted.
    [Fact]
    public void ReadCount_CountSplitAcrossLines_QuotesTheWholeStatus() =>
        MangaUpdatesVolumeCountParser.ReadCount([], " 12\nVolumes (Ongoing) ")
            .ShouldBe(new VolumeCountReading(12, VolumeCountBasis.MangaUpdatesStatus, "12\nVolumes (Ongoing)"));

    [Theory]
    [InlineData(null)]
    [InlineData("Ongoing")]
    public void ReadCount_NothingCountable_IsNull(string? status) =>
        MangaUpdatesVolumeCountParser.ReadCount([("Yen Audio", "14 Audiobooks; Ongoing")], status).ShouldBeNull();

    // The provenance must describe the very number the shelf has always used: the same count as
    // the English-edition rule, falling back to the status rule.
    [Theory]
    [InlineData("9 Volumes + 1 Extra - Complete", "10 Volumes (Complete)")]
    [InlineData("33 Digital Volumes; Complete", "33 Volumes (Complete)")]
    [InlineData(null, "26 LN Volumes (Complete)  \n24 Web Volumes (Complete)")]
    [InlineData("", "14 WN Volumes (Complete)")]
    [InlineData(null, "12\nVolumes (Ongoing)")]
    [InlineData(null, "23 LN\nVolumes (Complete)")]
    public void ReadCount_AnyText_CountsWhatTheOldRulesCounted(string? notes, string status) =>
        (MangaUpdatesVolumeCountParser.ReadCount([("Publisher", notes)], status)?.Count)
            .ShouldBe(MangaUpdatesVolumeCountParser.ParseEnglishEditions([notes]) ?? MangaUpdatesVolumeCountParser.Parse(status));
}
