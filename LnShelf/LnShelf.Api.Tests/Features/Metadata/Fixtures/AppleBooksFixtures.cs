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
internal static class AppleBooksFixtures
{
    public const string EightySixSearch = "apple-books-search-86-eighty-six.json";

    public static AppleBooksSearchResponse LoadSearch(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Features", "Metadata", "Fixtures", fileName);
        var json = File.ReadAllText(path);

        return JsonSerializer.Deserialize<AppleBooksSearchResponse>(json, AppleBooksClient.JsonOptions)
               ?? throw new InvalidOperationException($"Fixture '{fileName}' deserialized to null.");
    }
}
