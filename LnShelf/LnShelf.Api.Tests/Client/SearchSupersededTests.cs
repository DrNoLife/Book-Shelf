using LnShelf.Client.Features.Search;

namespace LnShelf.Api.Tests.Client;

/// <summary>
/// A search started while another is in flight replaces it. Under AniList's degraded rate
/// limit the older answer can arrive last, and it must not land under the newer query or
/// under a box that has been cleared.
/// </summary>
public sealed class SearchSupersededTests
{
    [Fact]
    public async Task SearchAsync_AnOlderSearchAnswersLast_ShowsTheNewerResults()
    {
        using var handler = new ScriptedHttpHandler(Answer);
        var state = CreateState(handler);
        var older = handler.Hold(HttpMethod.Get, "/api/search");
        var newer = handler.Hold(HttpMethod.Get, "/api/search");

        state.Query = "overlord";
        var olderSearch = state.SearchAsync();
        await older.Arrived;
        state.Query = "tanya";
        var newerSearch = state.SearchAsync();
        await newer.Arrived;

        older.Release();
        await olderSearch;

        // The older search ending must not turn off the newer one's spinner.
        state.IsSearching.ShouldBeTrue();

        newer.Release();
        await newerSearch;

        state.IsSearching.ShouldBeFalse();
        state.Results.ShouldHaveSingleItem().TitleEnglish.ShouldBe("tanya");
    }

    [Fact]
    public async Task SearchAsync_TheBoxIsClearedMidSearch_StaysEmpty()
    {
        using var handler = new ScriptedHttpHandler(Answer);
        var state = CreateState(handler);
        var held = handler.Hold(HttpMethod.Get, "/api/search");

        state.Query = "overlord";
        var search = state.SearchAsync();
        await held.Arrived;
        state.Query = string.Empty;
        await state.SearchAsync();

        held.Release();
        await search;

        state.Results.ShouldBeEmpty();
        state.HasSearched.ShouldBeFalse();
        state.IsSearching.ShouldBeFalse();
    }

    private static SeriesSearchState CreateState(ScriptedHttpHandler handler)
    {
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://api/") };

        return new SeriesSearchState(client, new SeriesAddClient(client));
    }

    // One result per search, titled with the query that asked for it, so a result can be
    // traced back to its search.
    private static HttpResponseMessage Answer(HttpRequestMessage request)
    {
        var uri = request.RequestUri.ShouldNotBeNull();

        if (uri.AbsolutePath == "/api/series/tracked")
        {
            return ScriptedHttpHandler.Json(new TrackedSeriesIdsResponse { Series = [] });
        }

        var query = Uri.UnescapeDataString(uri.Query["?q=".Length..]);

        return ScriptedHttpHandler.Json(new SeriesSearchResponse
        {
            Source = MetadataSourceName.AniList,
            Results =
            [
                new SeriesSearchResultResponse
                {
                    Source = MetadataSourceName.AniList,
                    AniListId = query.Length,
                    TitleEnglish = query,
                    Format = Format.LightNovel,
                    PublicationStatus = PublicationStatus.Ongoing,
                },
            ],
        });
    }
}
