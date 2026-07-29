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
}
