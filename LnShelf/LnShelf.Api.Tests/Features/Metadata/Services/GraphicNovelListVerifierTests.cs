using LnShelf.Api.Tests.Features.Metadata.Fixtures;

namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// Which table of a page is this series: by a shared English ISBN, never by title.
/// </summary>
public sealed class GraphicNovelListVerifierTests
{
    // Slime's page lists the light novels first; a manga series is its own table, not theirs.
    [Theory]
    [InlineData(WikipediaFixtures.SlimeMangaVolumeOneIsbn, 1)]
    [InlineData(WikipediaFixtures.SlimeLightNovelVolumeOneIsbn, 0)]
    public void Pick_MultiSectionPage_TakesTheTableHoldingTheSeriesIsbn(string isbn, int expectedTable)
    {
        var tables = WikipediaFixtures.Tables(WikipediaFixtures.SlimePage);

        var picked = GraphicNovelListVerifier.Pick(tables, new HashSet<string> { isbn });

        picked.ShouldBeSameAs(tables[expectedTable]);
    }

    // The e-book's ISBN against a page of print ISBNs: a miss, accepted, never a title guess.
    [Fact]
    public void Pick_NoSharedIsbn_IsNull()
    {
        var tables = WikipediaFixtures.Tables(WikipediaFixtures.ChainsawManPage);

        GraphicNovelListVerifier.Pick(tables, new HashSet<string> { "9781974717231" }).ShouldBeNull();
    }

    // An older page writes the English edition's ISBN-10; the shelf holds 13 digits.
    [Fact]
    public void Pick_PageWritesIsbnTen_MatchesTheThirteenDigitForm()
    {
        const string wikitext = """
            {{Graphic novel list/header|Language=Japanese}}
            {{Graphic novel list
            | VolumeNumber = 1
            | LicensedISBN = 1-59307-020-9<ref>ISBN 978-0-00-000000-2 in a citation</ref>
            | ChapterList = * 001. "The Black Swordsman"
            }}
            """;

        var tables = GraphicNovelListParser.Parse(wikitext);

        tables[0].Entries[0].LicensedIsbns.ShouldBe([WikipediaFixtures.BerserkVolumeOneIsbn]);
        GraphicNovelListVerifier.Pick(tables, new HashSet<string> { WikipediaFixtures.BerserkVolumeOneIsbn }).ShouldNotBeNull();
    }

    // Bonus volumes and rows without numbered chapters are not part of the seam's answer.
    [Fact]
    public void ToChaptersByVolume_Berserk_LeavesOutUnnumberedVolumes()
    {
        var table = WikipediaFixtures.Tables(WikipediaFixtures.BerserkPage)[0];

        var chapters = GraphicNovelListVerifier.ToChaptersByVolume(table);

        chapters.Keys.Order().First().ShouldBe(5);
        chapters.Count.ShouldBe(39);
        chapters[5][0].ShouldBe(1m);
    }

    // Slime's manga runs to vol 33 in Japan, whose chapter list is still empty.
    [Fact]
    public void ToChaptersByVolume_SlimeManga_MapsEveryVolumeThatListsItsChapters()
    {
        var manga = WikipediaFixtures.Tables(WikipediaFixtures.SlimePage)[1];

        var chapters = GraphicNovelListVerifier.ToChaptersByVolume(manga);

        chapters.Keys.Order().ShouldBe(Enumerable.Range(1, 32));
        chapters[29][0].ShouldBe(125m);
    }
}
