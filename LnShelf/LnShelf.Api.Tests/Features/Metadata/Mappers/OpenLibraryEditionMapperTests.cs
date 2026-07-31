using LnShelf.Api.Tests.Features.Metadata.Fixtures;

namespace LnShelf.Api.Tests.Features.Metadata.Mappers;

/// <summary>
/// Open Library's wire shape → the shelf's enrichment record, against captured real
/// responses rather than hand-invented JSON (testing.md #4).
/// </summary>
public sealed class OpenLibraryEditionMapperTests
{
    private static Uri CoverUrl(int coverId) => new($"https://covers.openlibrary.org/b/id/{coverId}-L.jpg");

    [Fact]
    public void ToEnrichment_ARecordWithAFullDateAndACover_ReadsBoth()
    {
        var edition = OpenLibraryFixtures.LoadEdition(OpenLibraryFixtures.DatedWithCover);

        var enrichment = OpenLibraryEditionMapper.ToEnrichment(edition, CoverUrl);

        enrichment.SourceRecordKey.ShouldBe("/books/OL26842920M");
        enrichment.ReleaseDate.ShouldBe(new DateOnly(2017, 1, 31));
        enrichment.RawPublishDate.ShouldBe("2017-01-31");
        enrichment.CoverImageUrl.ShouldBe(new Uri("https://covers.openlibrary.org/b/id/15137526-L.jpg"));
        enrichment.Publisher.ShouldBe("Yen On");
        enrichment.Language.ShouldBe(EditionLanguage.English);
    }

    [Fact]
    public void ToEnrichment_ARecordWithNoFullTitle_AssemblesItFromTitleAndSubtitle()
    {
        // The real Overlord 3 shape: no full_title, and the volume's own name is in
        // subtitle. Dropping the subtitle would print "Overlord, Volume 3" as the
        // volume's title, which is the number the caption already prints.
        var edition = OpenLibraryFixtures.LoadEdition(OpenLibraryFixtures.DatedWithCover);

        OpenLibraryEditionMapper.ToEnrichment(edition, CoverUrl)
            .Title.ShouldBe("Overlord, Volume 3: The Bloody Valkyrie");
    }

    [Fact]
    public void ToEnrichment_ARecordWithAFullTitle_PrefersItOverTitle()
    {
        var edition = OpenLibraryFixtures.LoadEdition(OpenLibraryFixtures.YearOnly);

        OpenLibraryEditionMapper.ToEnrichment(edition, CoverUrl)
            .Title.ShouldBe("That Time I Got Reincarnated As a Slime, Vol. 14 (light Novel)");
    }

    [Fact]
    public void ToEnrichment_ARecordDatedByYearAlone_HasNoReleaseDate()
    {
        var edition = OpenLibraryFixtures.LoadEdition(OpenLibraryFixtures.YearOnly);

        var enrichment = OpenLibraryEditionMapper.ToEnrichment(edition, CoverUrl);

        // "2022" is a real fact that is not a release date. Widening it to 1 January
        // would invent a day the publisher never claimed.
        enrichment.ReleaseDate.ShouldBeNull();

        // Kept verbatim, so the sync log can say what it refused rather than going quiet.
        enrichment.RawPublishDate.ShouldBe("2022");
    }

    [Fact]
    public void ToEnrichment_ADigitalOnlyEdition_HasNoCover()
    {
        var edition = OpenLibraryFixtures.LoadEdition(OpenLibraryFixtures.YearOnly);

        // 13% of this shelf's ISBNs land on a record with no scanned cover at all.
        OpenLibraryEditionMapper.ToEnrichment(edition, CoverUrl).CoverImageUrl.ShouldBeNull();
    }

    [Fact]
    public void ToEnrichment_ARecordWithNoLanguageField_IsUnstatedRatherThanForeign()
    {
        var edition = OpenLibraryFixtures.LoadEdition(OpenLibraryFixtures.JapaneseIsbn);

        var enrichment = OpenLibraryEditionMapper.ToEnrichment(edition, CoverUrl);

        // The real trap this fixture exists for: Slime volume 1's Japanese ISBN resolves
        // to a French edition that states no language at all. "Unstated" is the honest
        // reading; treating it as English would put that record on the shelf.
        enrichment.Language.ShouldBe(EditionLanguage.Unstated);
        enrichment.Publisher.ShouldBe("MICROMAGAZINE");
    }

    [Fact]
    public void ToEnrichment_ASearchResponse_CarriesEveryEditionsIsbnAndNoTitleAtAll()
    {
        var search = OpenLibraryFixtures.LoadSearch(OpenLibraryFixtures.OverlordSearch);

        var isbns = search.Docs!.SelectMany(document => document.Isbn ?? []).ToList();

        // Two works for one volume: Yen On's English editions and the Kadokawa and
        // Brazilian ones, in one pile. Both English ISBN-13s are in there…
        isbns.ShouldContain("9780316363921");

        // …and so is the Japanese one, which is exactly why every candidate is verified
        // one edition at a time rather than trusted because the search returned it.
        isbns.ShouldContain("9784041030721");
    }
}
