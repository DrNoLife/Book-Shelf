using LnShelf.Api.Features.Metadata.Models.AppleBooks;
using LnShelf.Api.Tests.Features.Metadata.Fixtures;

namespace LnShelf.Api.Tests.Features.Metadata.Mappers;

/// <summary>
/// Apple's wire shape → the shelf's catalogue records, against a captured real response
/// rather than hand-invented JSON (testing.md #4).
/// </summary>
public sealed class AppleBooksCatalogueMapperTests
{
    private static IReadOnlyList<CatalogueVolume> Catalogue() =>
        AppleBooksCatalogueMapper.ToCatalogueVolumes(
            AppleBooksFixtures.LoadSearch(AppleBooksFixtures.EightySixSearch));

    private static CatalogueVolume Volume(string title) =>
        Catalogue().Single(volume => volume.Title == title);

    [Fact]
    public void ToCatalogueVolumes_ARowThatIsNotAnEbook_IsDropped()
    {
        var catalogue = Catalogue();

        // Six of the seven captured rows are ebooks; the seventh is an audiobook, which
        // carries no `kind` at all. A negative test ("not an audiobook") would let it
        // through and put an audiobook's date on a light novel.
        catalogue.Count.ShouldBe(6);
        catalogue.ShouldAllBe(volume => volume.Title != "86--EIGHTY-SIX, Vol. 4");
    }

    [Fact]
    public void ToCatalogueVolumes_ATrackName_IsCarriedVerbatim()
    {
        // Verbatim including the "(light novel)" marker, which is the only thing in the
        // record that positively states the medium — trimming it here would disarm
        // VolumeEditionMatcher.ConfirmsMedium downstream.
        Volume("86--EIGHTY-SIX, Vol. 8 (light novel)").Title
            .ShouldBe("86--EIGHTY-SIX, Vol. 8 (light novel)");
    }

    [Fact]
    public void ToCatalogueVolumes_ATrackId_BecomesTheRecordKey()
    {
        // Apple's own id, which is stable across every request for that listing — the
        // artwork URL is only the fallback for a row that has none.
        Volume("86--EIGHTY-SIX, Vol. 8 (light novel)").SourceRecordKey.ShouldBe("1560603224");
    }

    [Fact]
    public void ToCatalogueVolumes_AReleaseDate_ReadsTheDayApplePrinted()
    {
        // "2021-08-24T07:00:00Z" is midnight on the 24th in the storefront's own timezone.
        // Parsing it as an instant and asking a local clock for the day would move the
        // release to the 23rd or the 25th depending on where the container runs.
        Volume("86--EIGHTY-SIX, Vol. 8 (light novel)").ReleaseDate.ShouldBe(new DateOnly(2021, 8, 24));
    }

    [Fact]
    public void ToCatalogueVolumes_APreorder_KeepsItsFutureDate()
    {
        // A storefront lists preorders, so a forward date is ordinary business here. What
        // may be *written* from it is the conflict rules' decision, not the mapper's.
        Volume("86--EIGHTY-SIX, Vol. 14 (light novel)").ReleaseDate.ShouldBe(new DateOnly(2026, 12, 8));
    }

    [Fact]
    public void ToCatalogueVolumes_ADescription_ArrivesAsPlainText()
    {
        var description = Volume("86 – EIGHTY-SIX (deutsche Light Novel): Band 1")
            .Description.ShouldNotBeNull();

        // The blurb really does arrive as HTML, so the shared sanitizer runs at this edge
        // and nothing inward ever holds markup.
        description.ShouldNotContain("<br />");
        description.ShouldContain("den sicheren Tod.\nShin war ein schweigsamer Junge");
    }

    [Fact]
    public void ToCatalogueVolumes_AnArtworkThumbnail_IsUpgradedToCoverSize()
    {
        // The size is a path segment the CDN honours as a fit-inside box: 1000x1500 on a
        // 2:3 cover returns a real 1000x1500 JPEG rather than an upscaled thumbnail.
        Volume("86--EIGHTY-SIX, Vol. 8 (light novel)").ArtworkUrl.ShouldBe(
            new Uri(
                "https://is1-ssl.mzstatic.com/image/thumb/Publication221/v4/16/e2/40/"
                + "16e240f6-0ea7-ceb3-4907-234c5f20018a/9781975320775.jpg/1000x1500bb.jpg"));
    }

    [Fact]
    public void ToCatalogueVolume_AnArtworkUrlOfAnUnexpectedShape_IsKeptAsItIs()
    {
        // The swap is a string edit on a URL shape Apple owns. A row that does not carry
        // that shape still has usable art at whatever size it came in.
        var mapped = AppleBooksCatalogueMapper.ToCatalogueVolume(new AppleBooksSearchResult
        {
            TrackId = 1,
            TrackName = "Overlord, Vol. 3 (light novel)",
            ArtworkUrl100 = "https://is1-ssl.mzstatic.com/image/thumb/abc.jpg/512x512bb.jpg",
        });

        mapped.ShouldNotBeNull().ArtworkUrl
            .ShouldBe(new Uri("https://is1-ssl.mzstatic.com/image/thumb/abc.jpg/512x512bb.jpg"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ToCatalogueVolume_ARowWithNoTitle_MapsToNothing(string? trackName) =>
        // The title is the only evidence of which volume a record is, so a row without one
        // cannot be placed and is not worth carrying inward.
        AppleBooksCatalogueMapper.ToCatalogueVolume(new AppleBooksSearchResult
        {
            TrackId = 1,
            TrackName = trackName,
        })
            .ShouldBeNull();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("soon")]
    [InlineData("2021-13-45T07:00:00Z")]
    public void ToCatalogueVolume_ADateThatIsNotOne_ReadsAsNoDate(string? releaseDate) =>
        AppleBooksCatalogueMapper.ToCatalogueVolume(new AppleBooksSearchResult
        {
            TrackId = 1,
            TrackName = "Overlord, Vol. 3 (light novel)",
            ReleaseDate = releaseDate,
        })
            .ShouldNotBeNull()
            .ReleaseDate.ShouldBeNull();
}
