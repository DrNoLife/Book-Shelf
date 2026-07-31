namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// Open Library's <c>publish_date</c> is free text, and the shelf's release date is a
/// day. This is the line between them.
/// </summary>
public sealed class OpenLibraryPublishDateParserTests
{
    [Theory]
    // All three shapes below came out of one sample of this shelf's own ISBNs.
    [InlineData("2017-01-31", 2017, 1, 31)]
    [InlineData("Apr 24, 2018", 2018, 4, 24)]
    [InlineData("Jun 06, 2014", 2014, 6, 6)]
    [InlineData("September 24, 2019", 2019, 9, 24)]
    [InlineData("24 April 2018", 2018, 4, 24)]
    public void Parse_ADateNamingAWholeDay_IsThatDay(string raw, int year, int month, int day) =>
        OpenLibraryPublishDateParser.Parse(raw).ShouldBe(new DateOnly(year, month, day));

    [Theory]
    // A year, or a year and a month, is a real fact that is not a release date. The
    // shelf already says "out, date unknown" honestly, with IsReleased.
    [InlineData("2022")]
    [InlineData("2019-09")]
    [InlineData("September 2019")]
    [InlineData("199?")]
    [InlineData("no date")]
    [InlineData("")]
    [InlineData(null)]
    public void Parse_AnythingCoarserThanADay_IsRefused(string? raw) =>
        OpenLibraryPublishDateParser.Parse(raw).ShouldBeNull();

    [Fact]
    public void Parse_APaddedDate_IsStillRead() =>
        OpenLibraryPublishDateParser.Parse("  2017-01-31  ").ShouldBe(new DateOnly(2017, 1, 31));
}
