using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LnShelf.Shared;
using LnShelf.Shared.Metadata;
using LnShelf.Shared.Shelf;

namespace LnShelf.Client.Features.Search;

/// <summary>
/// The add-series flow's state container: one search, one add call.
/// </summary>
// Behind the Search & add page. The page stays presentational (blazor.md) —
// everything non-visual lives here, which is why the provisional panel this
// replaced could be deleted without touching a line of it.
public sealed class SeriesSearchState(HttpClient httpClient)
{
    public string Query { get; set; } = string.Empty;

    public IReadOnlyList<SeriesSearchResultResponse> Results { get; private set; } = [];

    /// <summary>Which source answered the last search. The page names it.</summary>
    public MetadataSourceName Source { get; private set; } = MetadataSourceName.AniList;

    /// <summary>
    /// One quiet line, set only when the usual source could not be reached and another one
    /// answered instead. Null on a normal search.
    /// </summary>
    public string? DegradedNote { get; private set; }

    public bool IsBusy { get; private set; }

    /// <summary>What just happened, in one line, for the page to print.</summary>
    public string? Message { get; private set; }

    public bool HasSearched { get; private set; }

    public event Action? Changed;

    public async Task SearchAsync(CancellationToken cancellationToken = default)
    {
        var query = Query.Trim();
        if (query.Length == 0)
        {
            Results = [];
            HasSearched = false;
            Message = null;
            DegradedNote = null;
            Changed?.Invoke();
            return;
        }

        IsBusy = true;
        Message = null;
        Changed?.Invoke();

        try
        {
            // GetAsync rather than GetFromJsonAsync, which throws on a failure status and
            // discards the body with it. The body is the useful half: the API answers 503
            // with the source's own words — "The AniList API has been temporarily disabled
            // due to severe stability issues" — and printing
            // "net_http_message_not_success_statuscode_reason" instead is what this replaces.
            using var response = await httpClient.GetAsync(
                $"api/search?q={Uri.EscapeDataString(query)}", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                await FailAsync(response, cancellationToken);
                return;
            }

            var payload = await response.Content.ReadFromJsonAsync<SeriesSearchResponse>(cancellationToken);

            Results = payload?.Results ?? [];
            Source = payload?.Source ?? MetadataSourceName.AniList;
            DegradedNote = payload?.DegradedNote;
            HasSearched = true;
        }
        catch (HttpRequestException exception)
        {
            // No answer at all — the API itself is unreachable. Rendering the failure is
            // handling it; there is nothing else the browser can do about it.
            Results = [];
            DegradedNote = null;
            HasSearched = true;
            Message = $"Search failed: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
            Changed?.Invoke();
        }
    }

    /// <returns>Null when the add failed; the page reads <see cref="Message"/> then.</returns>
    public async Task<AddSeriesResponse?> AddAsync(
        SeriesSearchResultResponse result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);

        IsBusy = true;
        Message = null;
        Changed?.Invoke();

        try
        {
            // Whichever id the row is carrying, and only that one: the API requires exactly
            // one, because the two are different catalogues' names for the series.
            var request = new AddSeriesRequest
            {
                AniListId = result.AniListId,
                MangaUpdatesId = result.MangaUpdatesId,
                Format = result.Format,
            };

            using var response = await httpClient.PostAsJsonAsync("api/series", request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var problem = await ReadProblemAsync(response, cancellationToken);

                Message = problem is null
                    ? $"Could not add {result.TitleEnglish}: {(int)response.StatusCode} {response.StatusCode}."
                    : $"Could not add {result.TitleEnglish}: {problem}";

                return null;
            }

            var added = await response.Content.ReadFromJsonAsync<AddSeriesResponse>(cancellationToken);
            if (added is null)
            {
                Message = $"Could not add {result.TitleEnglish}: the API returned no body.";
                return null;
            }

            // The 200-vs-201 distinction is the whole point of the idempotent add,
            // so the line says which one happened.
            Message = added.AlreadyTracked || response.StatusCode is not HttpStatusCode.Created
                ? $"{added.TitleEnglish} was already on the shelf ({added.VolumeCount} volumes) — nothing added."
                : $"Added {added.TitleEnglish} ({added.Format}) with {added.VolumeCount} volumes.";

            return added;
        }
        catch (HttpRequestException exception)
        {
            Message = $"Could not add {result.TitleEnglish}: {exception.Message}";
            return null;
        }
        finally
        {
            IsBusy = false;
            Changed?.Invoke();
        }
    }

    private async Task FailAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        Results = [];
        DegradedNote = null;
        HasSearched = true;

        var problem = await ReadProblemAsync(response, cancellationToken);

        Message = problem is null
            ? $"Search failed: {(int)response.StatusCode} {response.StatusCode}."
            : $"Search failed: {problem}";
    }

    /// <summary>
    /// The API's own account of the failure, or null when it did not give one.
    /// </summary>
    // Best-effort by design: a proxy or a dev-server can answer a failure with HTML, and a
    // second failure while reading the first must not replace a bad status code with a
    // deserialization message nobody can act on.
    private static async Task<string?> ReadProblemAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ApiProblemResponse>(cancellationToken);

            return problem?.Message;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or NotSupportedException)
        {
            return null;
        }
    }
}
