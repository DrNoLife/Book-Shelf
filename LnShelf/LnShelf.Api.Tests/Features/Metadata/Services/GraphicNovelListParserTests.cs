using LnShelf.Api.Tests.Features.Metadata.Fixtures;

namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// Captured Wikipedia list pages into rows, one test per chapter-list format the pages use.
/// </summary>
public sealed class GraphicNovelListParserTests
{
    // Two {{Numbered list}} columns per row, the second continuing the first's count.
    [Fact]
    public void Parse_ChainsawMan_CountsNumberedListsFromTheirStart()
    {
        var table = WikipediaFixtures.Tables(WikipediaFixtures.ChainsawManPage).ShouldHaveSingleItem();

        table.Entries.Count.ShouldBe(24);
        Entry(table, 1).Chapters.ShouldBe(Chapters(1, 7));
        Entry(table, 1).LicensedIsbns.ShouldBe([WikipediaFixtures.ChainsawManVolumeOneIsbn]);
        Entry(table, 20).Chapters.ShouldBe(Chapters(176, 186));
        Entry(table, 24).Chapters.ShouldBe(Chapters(223, 232));

        // Not licensed yet: no English ISBN, but its chapters are known.
        Entry(table, 24).LicensedIsbns.ShouldBeEmpty();
    }

    [Fact]
    public void Parse_Frieren_ReadsChapterListAndItsSecondColumnTogether()
    {
        var table = WikipediaFixtures.Tables(WikipediaFixtures.FrierenPage).ShouldHaveSingleItem();

        Entry(table, 1).Chapters.ShouldBe(Chapters(1, 7));
        Entry(table, 15).Chapters.ShouldBe(Chapters(138, 147));
    }

    // "*Mission: 6–11" then "*Extra Mission: 1": the extras count on their own scale.
    [Fact]
    public void Parse_SpyFamily_TakesTheFirstLabelledRangeOnly()
    {
        var table = WikipediaFixtures.Tables(WikipediaFixtures.SpyFamilyPage).ShouldHaveSingleItem();

        Entry(table, 1).Chapters.ShouldBe(Chapters(1, 5));
        Entry(table, 2).Chapters.ShouldBe(Chapters(6, 11));
        Entry(table, 17).Chapters.ShouldBe(Chapters(119, 127));

        // Announced, its chapter list still empty.
        Entry(table, 18).Chapters.ShouldBeNull();
    }

    // Berserk's first four volumes list their prologue chapters by title only; the numbering
    // starts at "001." in vol 5. A title that happens to hold a range ("(2–3)") is not one.
    [Fact]
    public void Parse_Berserk_ReadsLeadingNumbersAndLeavesUnnumberedVolumesUnmapped()
    {
        var table = WikipediaFixtures.Tables(WikipediaFixtures.BerserkPage).ShouldHaveSingleItem();

        Entry(table, 1).Chapters.ShouldBeNull();
        Entry(table, 2).Chapters.ShouldBeNull();
        Entry(table, 4).Chapters.ShouldBeNull();
        Entry(table, 5).Chapters.ShouldBe(Chapters(1, 6));
        Entry(table, 6).Chapters.ShouldBe(Chapters(7, 16));
        Entry(table, 43).Chapters.ShouldBe(Chapters(374, 382));
        Entry(table, 1).LicensedIsbns.ShouldBe([WikipediaFixtures.BerserkVolumeOneIsbn]);
    }

    // One table per {{Graphic novel list/header}}: the light novels, then each manga.
    [Fact]
    public void Parse_Slime_SplitsThePageIntoItsTables()
    {
        var tables = WikipediaFixtures.Tables(WikipediaFixtures.SlimePage);

        tables.Count.ShouldBe(9);
        tables[0].Entries[0].LicensedIsbns.ShouldBe([WikipediaFixtures.SlimeLightNovelVolumeOneIsbn]);
        tables[0].Entries.ShouldContain(entry => entry.VolumeNumber == 8.5m);
        Entry(tables[1], 1).LicensedIsbns.ShouldBe([WikipediaFixtures.SlimeMangaVolumeOneIsbn]);
        Entry(tables[1], 1).Chapters.ShouldBe(Chapters(1, 6));
        Entry(tables[1], 32).Chapters.ShouldBe(Chapters(137, 140));
    }

    private static GraphicNovelListEntry Entry(GraphicNovelListTable table, decimal volumeNumber) =>
        table.Entries.First(entry => entry.VolumeNumber == volumeNumber);

    private static List<decimal> Chapters(int first, int last) =>
        [.. Enumerable.Range(first, last - first + 1).Select(chapter => (decimal)chapter)];
}
