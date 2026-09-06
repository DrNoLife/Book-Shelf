namespace LnShelf.Api.Tests.Features.Import.Mappers;

/// <summary>
/// The dirty→clean edge for StoryGraph: the two fields where the file's own
/// shapes are irregular enough to lose data quietly (testing.md #4).
/// </summary>
public sealed class StoryGraphBookMapperTests
{
    /// <summary>
    /// Every date shape the "Dates Read" column is known to take, and the date the
    /// shelf should end up showing.
    /// </summary>
    public static TheoryData<string?, DateOnly?> DatesReadShapes => new()
    {
        // A range for a read that spanned days. The real export's Tanya volume 13
        // carries exactly this, and splitting the field on a comma parsed it to null.
        { "2026/07/21-2026/07/26", new DateOnly(2026, 7, 26) },

        { "2021/11/23", new DateOnly(2021, 11, 23) },
        { "2021/11/23, 2023/02/04", new DateOnly(2023, 2, 4) },

        // Out of order: the latest wins, not the last one written.
        { "2023/02/04, 2021/11/23", new DateOnly(2023, 2, 4) },

        // Dashes as the date separator as well as the range separator.
        { "2026-07-21-2026-07-26", new DateOnly(2026, 7, 26) },
        { "2021/11/23, 2023-02-04", new DateOnly(2023, 2, 4) },

        { null, null },
        { "", null },
        { "   ", null },

        // Nothing date-shaped in the field at all.
        { "not a date", null },
    };

    [Theory]
    [MemberData(nameof(DatesReadShapes))]
    public void ToBook_DatesRead_TakesTheLatestDateInTheField(string? datesRead, DateOnly? expected)
    {
        var row = new StoryGraphCsvRow
        {
            Title = "Overlord, Vol. 3",
            ReadStatus = "read",
            DatesRead = datesRead,
        };

        StoryGraphBookMapper.ToBook(row, lineNumber: 2).ReadAt.ShouldBe(expected);
    }

    [Fact]
    public void ToBook_BothDateColumns_PrefersLastDateRead()
    {
        var row = new StoryGraphCsvRow
        {
            Title = "Overlord, Vol. 3",
            ReadStatus = "read",
            LastDateRead = "2026/07/26",
            DatesRead = "2021/11/23, 2023/02/04",
        };

        StoryGraphBookMapper.ToBook(row, lineNumber: 2).ReadAt.ShouldBe(new DateOnly(2026, 7, 26));
    }

    [Fact]
    public void ToBook_NoLastDateRead_FallsBackToDatesRead()
    {
        var row = new StoryGraphCsvRow
        {
            Title = "Overlord, Vol. 3",
            ReadStatus = "read",
            LastDateRead = "",
            DatesRead = "2026/07/21-2026/07/26",
        };

        StoryGraphBookMapper.ToBook(row, lineNumber: 2).ReadAt.ShouldBe(new DateOnly(2026, 7, 26));
    }

    [Fact]
    public void ToBook_AnUnparseableLastDateRead_StillFallsBackToDatesRead()
    {
        var row = new StoryGraphCsvRow
        {
            Title = "Overlord, Vol. 3",
            ReadStatus = "read",
            LastDateRead = "unknown",
            DatesRead = "2021/11/23",
        };

        StoryGraphBookMapper.ToBook(row, lineNumber: 2).ReadAt.ShouldBe(new DateOnly(2021, 11, 23));
    }

    [Theory]
    [InlineData("Overlord, Vol. 3: The Bloody Valkyrie", "The Bloody Valkyrie")]
    [InlineData("The Saga of Tanya the Evil, Vol. 13: Dum Spiro, Spero: Part 1", "Dum Spiro, Spero: Part 1")]
    [InlineData("Overlord, Vol. 3", null)]
    [InlineData("Vesten mod vesten", null)]
    public void ToBook_AnEnglishSubtitle_IsCarried(string rawTitle, string? expected)
    {
        var row = new StoryGraphCsvRow { Title = rawTitle, ReadStatus = "read" };

        StoryGraphBookMapper.ToBook(row, lineNumber: 2).Subtitle.ShouldBe(expected);
    }

    [Theory]

    // The shape the real export writes: "Name (Role), Name (Role)", in whatever
    // order StoryGraph felt like.
    [InlineData("Roman Lempert (Translator), Shirabii (Illustrator)", "Shirabii")]
    [InlineData("Shirabii (Illustrator), Roman Lempert (Translator)", "Shirabii")]
    [InlineData("Shinobu Shinotsuki (Illustrator), Emily Balistrieri (Translator), Kevin Steinbach (Translator)", "Shinobu Shinotsuki")]

    // The role vocabulary is freeform community text.
    [InlineData("so-bin (Artist)", "so-bin")]
    [InlineData("so-bin (illustrator)", "so-bin")]

    // No role at all is as often an editor as the artist, and a name fed to the
    // tie-break has to be one AniList would credit as the artist.
    [InlineData("Mitz Vah, Kevin Gifford (Translator)", null)]
    [InlineData("Kevin Gifford (Translator)", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void ToBook_Contributors_CarriesTheCreditedIllustrator(string? contributors, string? expected)
    {
        var row = new StoryGraphCsvRow
        {
            Title = "86—EIGHTY-SIX, Vol. 1",
            ReadStatus = "read",
            Contributors = contributors,
        };

        StoryGraphBookMapper.ToBook(row, lineNumber: 2).Illustrator.ShouldBe(expected);
    }

    [Theory]
    [InlineData("オーバーロード1 不死者の王")]
    [InlineData("オーバーロード11 山小人の工匠")]
    public void ToBook_AJapaneseSubtitle_IsDropped(string rawTitle)
    {
        // The shelf entry is the English one, so a Japanese subtitle would be the
        // wrong text under an English volume — and it would block the enrichment that
        // knows the English one.
        var row = new StoryGraphCsvRow { Title = rawTitle, ReadStatus = "read" };

        StoryGraphBookMapper.ToBook(row, lineNumber: 2).Subtitle.ShouldBeNull();
    }
}
