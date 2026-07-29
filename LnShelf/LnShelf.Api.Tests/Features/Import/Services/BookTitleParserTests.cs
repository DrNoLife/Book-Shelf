namespace LnShelf.Api.Tests.Features.Import.Services;

/// <summary>
/// Title parsing, case by case, against the shapes a real StoryGraph export
/// actually contains (testing.md #2 — every mismatch found during the real import
/// becomes a regression case).
/// </summary>
public sealed class BookTitleParserTests
{
    /// <summary>
    /// Every distinct volume-numbering shape in the real 76-row export. All ten
    /// of these are verbatim rows from it.
    /// </summary>
    public static TheoryData<string, string, int?> RealExportTitles => new()
    {
        // The ordinary case.
        { "That Time I Got Reincarnated as a Slime, Vol. 5", "That Time I Got Reincarnated as a Slime", 5 },

        // A volume subtitle after the number, and a comma inside that subtitle.
        { "Overlord, Vol. 3: The Bloody Valkyrie", "Overlord", 3 },
        {
            "The Hero Laughs While Walking the Path of Vengeance a Second Time, Vol. 4: The Merchant, Mired in Greed",
            "The Hero Laughs While Walking the Path of Vengeance a Second Time", 4
        },

        // "Volume" spelled out — the same series as the first row, two rows apart.
        { "That Time I Got Reincarnated as a Slime, Volume 15", "That Time I Got Reincarnated as a Slime", 15 },

        // A colon in the series' own name, before any volume marker.
        { "Torture Princess: Fremd Torturchen, Vol. 2", "Torture Princess: Fremd Torturchen", 2 },

        // Part-numbered: the part belongs to the series' identity, because AniList
        // publishes "Ascendance of a Bookworm: Part 2" as its own series.
        { "Ascendance of a Bookworm: Part 2 Volume 3", "Ascendance of a Bookworm: Part 2", 3 },
        { "Ascendance of a Bookworm: Part 1 Volume 1", "Ascendance of a Bookworm: Part 1", 1 },

        // Japanese: the number runs straight onto the title, with and without a space.
        { "転生したらスライムだった件1", "転生したらスライムだった件", 1 },
        { "オーバーロード11 山小人の工匠", "オーバーロード", 11 },

        // A bracketed romanisation StoryGraph appends, and a trailing space inside
        // the quoted field.
        { "転生したらスライムだった件 13 [Tensei shitara Slime Datta Ken 13]", "転生したらスライムだった件", 13 },
        { "オーバーロード1 不死者の王 ", "オーバーロード", 1 },

        // An em dash in the series name, parentheses in the subtitle.
        { "86—EIGHTY-SIX, Vol. 3: Run Through the Battlefront (Finish)", "86—EIGHTY-SIX", 3 },

        // The same series written with two hyphens instead of an em dash.
        { "86--EIGHTY-SIX, Vol. 11: Dies Passionis", "86--EIGHTY-SIX", 11 },

        // Not a series at all: a standalone Danish novel, no volume anywhere.
        { "Vesten mod vesten", "Vesten mod vesten", null },
    };

    [Theory]
    [MemberData(nameof(RealExportTitles))]
    public void Parse_RealExportTitle_SplitsSeriesFromVolume(
        string rawTitle, string expectedSeriesTitle, int? expectedVolumeNumber)
    {
        var parsed = BookTitleParser.Parse(rawTitle);

        parsed.SeriesTitle.ShouldBe(expectedSeriesTitle);
        parsed.VolumeNumber.ShouldBe(expectedVolumeNumber);
    }

    [Fact]
    public void Parse_PartNumberedTitle_KeepsThePartNumberSeparatelyToo()
    {
        var parsed = BookTitleParser.Parse("Ascendance of a Bookworm: Part 2 Volume 3");

        parsed.Part.ShouldBe(2);
        parsed.VolumeNumber.ShouldBe(3);
    }

    [Fact]
    public void Parse_TwoPartsOfOneSeries_AreDifferentSeries()
    {
        // Part 1 volume 1 and Part 2 volume 1 are two different books, and would
        // collide on the shelf if the part were dropped.
        var first = BookTitleParser.Parse("Ascendance of a Bookworm: Part 1 Volume 1");
        var second = BookTitleParser.Parse("Ascendance of a Bookworm: Part 2 Volume 1");

        first.SeriesTitle.ShouldNotBe(second.SeriesTitle);
        first.VolumeNumber.ShouldBe(second.VolumeNumber);
    }

    [Theory]
    // The guard on the Japanese rule: an English title that happens to end in a
    // number must not be carved up into a series and a volume.
    [InlineData("Fahrenheit 451")]
    [InlineData("Catch 22")]
    [InlineData("2001")]
    public void Parse_LatinTitleEndingInDigits_IsNotSplit(string rawTitle)
    {
        var parsed = BookTitleParser.Parse(rawTitle);

        parsed.SeriesTitle.ShouldBe(rawTitle);
        parsed.VolumeNumber.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Parse_NoTitle_YieldsNothingRatherThanThrowing(string? rawTitle)
    {
        var parsed = BookTitleParser.Parse(rawTitle);

        parsed.SeriesTitle.ShouldBeEmpty();
        parsed.VolumeNumber.ShouldBeNull();
    }

    [Fact]
    public void Parse_VolumeSubtitle_IsKeptForDisplay()
    {
        BookTitleParser.Parse("Overlord, Vol. 3: The Bloody Valkyrie")
            .Subtitle.ShouldBe("The Bloody Valkyrie");
    }
}
