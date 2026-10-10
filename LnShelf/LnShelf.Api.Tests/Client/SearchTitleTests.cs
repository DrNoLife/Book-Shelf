using LnShelf.Client.Features.Search;

namespace LnShelf.Api.Tests.Client;

/// <summary>A search hit's title taken apart into stem, part and the rest.</summary>
public sealed class SearchTitleTests
{
    [Theory]
    [InlineData("Ascendance of a Bookworm: Part 2 – Apprentice Shrine Maiden", "Ascendance of a Bookworm", "Part 2", "Apprentice Shrine Maiden")]
    [InlineData("Ascendance of a Bookworm Part 3: Adopted Daughter of an Archduke", "Ascendance of a Bookworm", "Part 3", "Adopted Daughter of an Archduke")]
    [InlineData("Ascendance of a Bookworm: Part 1", "Ascendance of a Bookworm", "Part 1", null)]
    [InlineData("Classroom of the Elite: 2nd Season", "Classroom of the Elite", "Season 2", null)]
    [InlineData("Classroom of the Elite - Part IV", "Classroom of the Elite", "Part 4", null)]
    [InlineData("Honzuki no Gekokujou - Dai 5-bu Megami no Keshin", "Honzuki no Gekokujou", "Part 5", "Megami no Keshin")]
    public void Parse_ATitleNamingAPart_SplitsStemPartAndRest(string title, string stem, string part, string? rest)
    {
        var parsed = SearchTitle.Parse(title);

        parsed.StemText.ShouldBe(stem);
        parsed.PartLabel.ShouldBe(part);
        parsed.AfterPart.ShouldBe(rest);
    }

    [Theory]
    [InlineData("Mushoku Tensei: Jobless Reincarnation")]
    [InlineData("Part-Time Wizard")]
    [InlineData("The Book Thief")]
    [InlineData("The Book I Love")]
    public void Parse_NoPart_IsAllStem(string title)
    {
        var parsed = SearchTitle.Parse(title);

        parsed.PartLabel.ShouldBeNull();
        parsed.StemText.ShouldBe(title);
        parsed.Stem.ShouldBe(parsed.Segments);
    }

    [Theory]
    [InlineData("Mushoku Tensei: Roxy Gets Serious", "mushoku tensei")]
    [InlineData("The Saga of Tanya the Evil", "saga of tanya the evil")]
    [InlineData("86: Eighty-Six", "86 eighty six")]
    public void WorkKey_IsTheFirstSegmentUnlessItIsTooShort(string title, string expected) =>
        SearchTitle.Parse(title).WorkKey.ShouldBe(expected);

    [Fact]
    public void RemainderAfter_CutsAtTheSegmentAndTrimsItsSeparators() =>
        SearchTitle.Parse("Mushoku Tensei: Jobless Reincarnation ~Eris Sharpens Her Fangs~")
            .RemainderAfter(2)
            .ShouldBe("Eris Sharpens Her Fangs");
}
