using System.Text.Json;

namespace LnShelf.EndToEnd.Tests.Harness;

/// <summary>
/// Finds a seeded series' id through the same API the client uses, so a journey can open
/// a series page directly instead of depending on the library's default filter.
/// </summary>
public static class ShelfLookup
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<Guid> SeriesIdAsync(IPage page, string titleEnglish)
    {
        var response = await page.APIRequest.GetAsync("api/library?filter=All");
        response.Ok.ShouldBeTrue($"GET /api/library answered {response.Status}.");

        var library = JsonSerializer.Deserialize<LibraryResponse>(await response.TextAsync(), JsonOptions)
                      ?? throw new InvalidOperationException("The library response was empty.");

        return library.Cards.Single(card => card.TitleEnglish == titleEnglish).SeriesId;
    }
}
