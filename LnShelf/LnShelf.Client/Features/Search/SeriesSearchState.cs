using System.Net;
using System.Net.Http.Json;
using LnShelf.Shared.Metadata;
using LnShelf.Shared.Shelf;

namespace LnShelf.Client.Features.Search;

/// <summary>
/// The add-series flow's state container: one AniList search, one add call.
/// </summary>
// Behind the Search & add page. The page stays presentational (blazor.md) —
// everything non-visual lives here, which is why the provisional panel this
// replaced could be deleted without touching a line of it.
public sealed class SeriesSearchState(HttpClient httpClient)
{
    public string Query { get; set; } = string.Empty;

    public IReadOnlyList<SeriesSearchResultResponse> Results { get; private set; } = [];

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
            Changed?.Invoke();
            return;
        }

        IsBusy = true;
        Message = null;
        Changed?.Invoke();

        try
        {
            Results = await httpClient.GetFromJsonAsync<IReadOnlyList<SeriesSearchResultResponse>>(
                          $"api/search?q={Uri.EscapeDataString(query)}", cancellationToken)
                      ?? [];

            HasSearched = true;
        }
        catch (HttpRequestException exception)
        {
            // Rendering the failure is handling it; there is nothing else the
            // browser can do about an API or an AniList that is down.
            Results = [];
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
            var request = new AddSeriesRequest { AniListId = result.AniListId, Format = result.Format };
            var response = await httpClient.PostAsJsonAsync("api/series", request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                Message = $"Could not add {result.TitleEnglish}: {(int)response.StatusCode} {response.StatusCode}.";
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
}
