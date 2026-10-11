using System.Net;

namespace LnShelf.Client.Features.Search;

/// <summary>
/// POST api/series (or api/series/manual), answered with the one line the page prints about it.
/// </summary>
// Apart from SeriesSearchState so the state container keeps the orchestration — the row add,
// the add-all loop, the busy flags — and this keeps the wire: the request shape, the
// problem-details reading and the wording of each outcome.
public sealed class SeriesAddClient(HttpClient httpClient)
{
    /// <param name="start">The preview's "Start with"; null adds the series untouched.</param>
    public Task<SeriesAddOutcome> AddAsync(
        SeriesSearchResultResponse result,
        StartingVolumes? start,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);

        // Whichever id the row is carrying, and only that one: the API requires exactly
        // one, because the two are different catalogues' names for the series.
        var request = new AddSeriesRequest
        {
            AniListId = result.AniListId,
            MangaUpdatesId = result.MangaUpdatesId,
            Format = result.Format,
            InitialReadCount = start?.Read,
            InitialOwnedCount = start?.Owned,
        };

        return AddAsync(request, result.TitleEnglish, cancellationToken);
    }

    /// <summary>
    /// Adds by id from anywhere that is not a search row — the series page's "More by" tiles.
    /// </summary>
    /// <param name="title">What the outcome line calls the series if the add fails.</param>
    public Task<SeriesAddOutcome> AddAsync(
        AddSeriesRequest request,
        string title,
        CancellationToken cancellationToken) =>
        PostAsync("api/series", request, title, cancellationToken);

    /// <summary>"Can't find it? Add it by hand": a series no catalogue knows.</summary>
    public Task<SeriesAddOutcome> AddManualAsync(
        AddManualSeriesRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return PostAsync("api/series/manual", request, request.TitleEnglish, cancellationToken);
    }

    private async Task<SeriesAddOutcome> PostAsync<TRequest>(
        string route,
        TRequest request,
        string title,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.PostAsJsonAsync(route, request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var problem = await response.ReadProblemAsync(cancellationToken);

                return Failed(problem is null
                    ? $"Could not add {title}: {(int)response.StatusCode} {response.StatusCode}."
                    : $"Could not add {title}: {problem}");
            }

            var added = await response.Content.ReadFromJsonAsync<AddSeriesResponse>(cancellationToken);
            if (added is null)
            {
                return Failed($"Could not add {title}: the API returned no body.");
            }

            // The 200-vs-201 distinction is the whole point of the idempotent add,
            // so the line says which one happened.
            var message = added.AlreadyTracked || response.StatusCode is not HttpStatusCode.Created
                ? $"{added.TitleEnglish} was already on the shelf ({added.VolumeCount} volumes) — nothing added."
                : $"Added {added.TitleEnglish} ({SearchWorkText.LongName(added.Format).ToLowerInvariant()}) with {added.VolumeCount} volumes.";

            return new SeriesAddOutcome(added, message);
        }
        catch (HttpRequestException exception)
        {
            return Failed($"Could not add {title}: {exception.Message}");
        }
    }

    private static SeriesAddOutcome Failed(string message) => new(null, message);
}

/// <param name="Added">Null when the add failed.</param>
/// <param name="Message">What happened, in one line, for the page to print.</param>
public sealed record SeriesAddOutcome(AddSeriesResponse? Added, string Message);
