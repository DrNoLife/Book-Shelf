using System.Text.Json;
using LnShelf.Api.Features.Metadata.Models.OpenLibrary;

namespace LnShelf.Api.Tests.Features.Metadata.Fixtures;

/// <summary>
/// Captured real Open Library responses (testing.md #4).
/// </summary>
// Captured 2026-07-31 with the very requests OpenLibraryClient sends, using ISBNs from
// this shelf's own database:
//
//   open-library-edition-dated-with-cover.json   GET /isbn/9780316363983.json  (Overlord 3)
//   open-library-edition-year-only.json          GET /isbn/9781975314484.json  (Slime 14)
//   open-library-edition-japanese.json           GET /isbn/9784896374599.json  (Slime 1, JP ISBN)
//   open-library-search-overlord.json            GET /search.json?q="Overlord volume 2"&fields=key,isbn
//
// Every one of them is a specimen of a case that would otherwise be invented:
//
// - Overlord 3 has a full ISO date and a cover, but no `full_title` — its volume part
//   is in `subtitle`, so the title has to be assembled.
// - Slime 14 has `publish_date: "2022"`. A year is not a release date.
// - Slime 1's Japanese ISBN resolves to a *French* edition with no `languages` field at
//   all, which is why the ISBN's own registration group has to be the signal rather
//   than the record's language.
// - The Overlord search returns two works: the English editions and the Japanese and
//   Brazilian ones, in one undifferentiated pile. That pile is the whole reason
//   candidates are verified one edition at a time.
internal static class OpenLibraryFixtures
{
    public const string DatedWithCover = "open-library-edition-dated-with-cover.json";
    public const string YearOnly = "open-library-edition-year-only.json";
    public const string JapaneseIsbn = "open-library-edition-japanese.json";
    public const string OverlordSearch = "open-library-search-overlord.json";

    public static OpenLibraryEdition LoadEdition(string fileName) => Load<OpenLibraryEdition>(fileName);

    public static OpenLibrarySearchResponse LoadSearch(string fileName) =>
        Load<OpenLibrarySearchResponse>(fileName);

    private static TData Load<TData>(string fileName)
        where TData : class
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Features", "Metadata", "Fixtures", fileName);
        var json = File.ReadAllText(path);

        return JsonSerializer.Deserialize<TData>(json, OpenLibraryClient.JsonOptions)
               ?? throw new InvalidOperationException($"Fixture '{fileName}' deserialized to null.");
    }
}
