using System.Text.Json;
using LnShelf.Api.Features.Metadata.Models.AppleBooks;

namespace LnShelf.Api.Tests.Features.Metadata.Fixtures;

/// <summary>
/// A captured real iTunes Search response (testing.md #4).
/// </summary>
// Captured 2026-08-15 with the very request AppleBooksClient sends, for a series on this
// shelf:
//
//   apple-books-search-86-eighty-six.json
//       GET /search?term=86+eighty+six+light+novel&media=ebook&limit=25&country=us
//
// Trimmed from thirty results to seven, and every one kept is a specimen of a case that
// would otherwise be invented:
//
// - Vol. 14 is dated 2026-12-08, which is in the future. A storefront lists preorders, so
//   a forward date is ordinary business here rather than an error — and it is exactly the
//   conflict the release-date rule was written for.
// - Vol. 8 is the plain case: a past date, a blurb, and cover art.
// - Vol. 1 (light novel) and Vol. 1 (manga) are the same series, the same volume number
//   and different media, one row apart. This is the collision the matcher exists for.
// - "Alter.1" is a spin-off numbered from one. Its title starts with the series name and
//   puts a 1 straight after it, which is what a volume-number match looks like — so it is
//   the case that proves first-confirmed-wins depends on the store's own ranking.
// - The German "Band 1" row carries <br /> in its blurb, which is how the sanitizer earns
//   its second consumer.
// - The audiobook row is the one amendment, flagged rather than hidden: it comes from the
//   same capture session against media=audiobook, because a media=ebook response contains
//   no non-book rows to catch. Its shape is Apple's own and verbatim — no `kind` and no
//   `trackName` at all, only `wrapperType` and `collectionName`, which is precisely why
//   the filter has to be "says ebook" rather than "does not say audiobook".
//
// The trimmed document's resultCount was set to seven to match, so the fixture stays
// self-consistent.
//
// Deserialization goes through AppleBooksClient.JsonOptions, so the tests exercise
// production's own serializer configuration too.
//
// 2026-10-11, for whole extra volumes and matcher recall, each with the request
// AppleBooksClient sends today (limit=200) and trimmed to the rows named, verbatim, with
// resultCount set to match:
//
//   apple-books-search-solo-leveling-manga.json
//       GET /search?term=Solo+Leveling+manga&media=ebook&limit=200&country=us
//       Every row whose title starts "Solo Leveling": IZE Press' "Vol. 1 (comic)" to
//       "Vol. 15 (comic)" (14 and 15 dated 2025-12-30 and 2026-03-17, past the "13+2" the
//       release source counts), the "(novel)" volumes of the light novel, the Ragnarok
//       sequel ("Vol. 3" carries the coverArt.<hash>.png placeholder, "Vol. 2" real art
//       ahead of its date) and an unrelated game guide. Genre "Manga" only on Vols. 1–5.
//   apple-books-search-chainsaw-man-manga.json
//       GET /search?term=Chainsaw+Man+manga&media=ebook&limit=200&country=us
//       Viz titles with no medium marker at all; the genre "Manga" is the only evidence.
//       Vol. 23 is a placeholder, Vol. 22 real art two days before its date.
//   apple-books-search-classroom-of-the-elite-light-novel.json
//       GET /search?term=Classroom+of+the+Elite+light+novel&media=ebook&limit=200&country=us
//       "Classroom of the Elite (Light Novel) Vol. 1": the medium before the volume marker.
//   apple-books-search-ascendance-of-a-bookworm-light-novel.json
//       GET /search?term=Ascendance+of+a+Bookworm+light+novel&media=ebook&limit=200&country=us
//       Light-novel rows Apple files under the genre "Manga", which must not confirm them.
internal static class AppleBooksFixtures
{
    public const string EightySixSearch = "apple-books-search-86-eighty-six.json";

    public const string SoloLevelingSearch = "apple-books-search-solo-leveling-manga.json";

    public const string ChainsawManSearch = "apple-books-search-chainsaw-man-manga.json";

    public const string ClassroomOfTheEliteSearch = "apple-books-search-classroom-of-the-elite-light-novel.json";

    public const string BookwormSearch = "apple-books-search-ascendance-of-a-bookworm-light-novel.json";

    /// <summary>The fixture as the catalogue seam returns it: mapped, unfiltered, in Apple's order.</summary>
    public static IReadOnlyList<CatalogueVolume> LoadCatalogue(string fileName) =>
        AppleBooksCatalogueMapper.ToCatalogueVolumes(LoadSearch(fileName));

    public static AppleBooksSearchResponse LoadSearch(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Features", "Metadata", "Fixtures", fileName);
        var json = File.ReadAllText(path);

        return JsonSerializer.Deserialize<AppleBooksSearchResponse>(json, AppleBooksClient.JsonOptions)
               ?? throw new InvalidOperationException($"Fixture '{fileName}' deserialized to null.");
    }
}
