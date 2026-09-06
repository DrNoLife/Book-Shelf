using System.Net;

namespace LnShelf.Client.Features.Series;

/// <summary>
/// Every call the series-detail page makes, and nothing else — no flags, no
/// notification, no page state.
/// </summary>
// A refused write is reported by throwing an HttpRequestException carrying the sentence
// to print. That is deliberate: the caller already had to catch that exception for the
// unreachable-API case, so one catch now covers both, and the wording for an endpoint
// stays next to the call to it rather than in the state container.
//
// Every write answers with the whole series. The counts, the progress bar and the
// next-to-buy CTA are all derived from the volume set, so a response carrying one row
// would leave the client deriving the rest — the duplication CLAUDE.md forbids.
public sealed class SeriesDetailClient(HttpClient httpClient)
{
    public Task<SeriesDetailResponse?> GetAsync(
        Guid seriesId,
        CancellationToken cancellationToken = default) =>
        httpClient.GetFromJsonAsync<SeriesDetailResponse>($"api/series/{seriesId}", cancellationToken);

    public Task<SeriesDetailResponse?> SetReadAsync(
        Guid volumeId,
        ReadStatus status,
        CancellationToken cancellationToken = default) =>
        WriteAsync(
            $"api/volumes/{volumeId}/read",
            new SetVolumeReadRequest { Status = status },
            "Could not save that",
            cancellationToken);

    public Task<SeriesDetailResponse?> SetOwnedAsync(
        Guid volumeId,
        bool owned,
        OwnedFormat? format,
        CancellationToken cancellationToken = default) =>
        WriteAsync(
            $"api/volumes/{volumeId}/owned",
            new SetVolumeOwnedRequest { Owned = owned, Format = format },
            "Could not save that",
            cancellationToken);

    /// <summary>"Mark 1–N read" / "Mark 1–N owned".</summary>
    public async Task<SeriesDetailResponse?> MarkThroughAsync(
        Guid seriesId,
        int throughNumber,
        ReadStatus? read,
        bool? owned,
        CancellationToken cancellationToken = default)
    {
        var request = new BulkVolumeStateRequest
        {
            ThroughNumber = throughNumber,
            Read = read,
            Owned = owned,
        };

        using var response = await httpClient.PostAsJsonAsync(
            $"api/series/{seriesId}/volumes/bulk", request, cancellationToken);

        return await ReadDetailAsync(response, "Could not apply that", cancellationToken);
    }

    /// <summary>
    /// Sets this series' volume view, or clears the override with null so it follows the
    /// global preference again.
    /// </summary>
    public Task<SeriesDetailResponse?> SetVolumeViewOverrideAsync(
        Guid seriesId,
        VolumeView? view,
        CancellationToken cancellationToken = default) =>
        WriteAsync(
            $"api/series/{seriesId}/volume-view",
            new SetSeriesVolumeViewRequest { View = view },
            "Could not save that view",
            cancellationToken);

    /// <summary>
    /// Sets the tracking status, the note, or both. A null field is left alone; an empty
    /// note clears it.
    /// </summary>
    public Task<SeriesDetailResponse?> SetStateAsync(
        Guid seriesId,
        TrackingStatus? trackingStatus,
        string? notes,
        CancellationToken cancellationToken = default) =>
        WriteAsync(
            $"api/series/{seriesId}/state",
            new UpdateSeriesStateRequest { TrackingStatus = trackingStatus, Notes = notes },
            "Could not save that",
            cancellationToken);

    /// <summary>
    /// Acknowledges the new-volume badge, so the card stops glowing on the way back to
    /// the library.
    /// </summary>
    // A 404 is not a failure worth reporting: it means the series is gone, and the badge
    // went with it. Nothing is read from the response — the endpoint answers 204.
    public async Task MarkSeenAsync(Guid seriesId, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsync(
            $"api/series/{seriesId}/seen", content: null, cancellationToken);

        if (!response.IsSuccessStatusCode && response.StatusCode is not HttpStatusCode.NotFound)
        {
            throw Refused("Could not clear the new-volume badge", response);
        }
    }

    public async Task DeleteAsync(Guid seriesId, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.DeleteAsync($"api/series/{seriesId}", cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return;
        }

        throw response.StatusCode is HttpStatusCode.NotFound
            ? new HttpRequestException("That series is not on the shelf any more.")
            : Refused("Could not delete that series", response);
    }

    /// <summary>
    /// Uploads a cover override. Multipart rather than JSON because the payload is
    /// image bytes, and the endpoint caps the size (feature-spec §4.3).
    /// </summary>
    public async Task<SeriesDetailResponse?> SetCoverAsync(
        Guid volumeId,
        Stream content,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        using var form = new MultipartFormDataContent();
        using var streamContent = new StreamContent(content);
        form.Add(streamContent, "file", fileName);

        using var response = await httpClient.PutAsync(
            $"api/volumes/{volumeId}/cover", form, cancellationToken);

        if (response.StatusCode is HttpStatusCode.BadRequest)
        {
            throw new HttpRequestException("That file is not an image the shelf can read.");
        }

        return await ReadDetailAsync(response, "Could not set that cover", cancellationToken);
    }

    public async Task<SeriesDetailResponse?> RemoveCoverAsync(
        Guid volumeId,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.DeleteAsync(
            $"api/volumes/{volumeId}/cover", cancellationToken);

        return await ReadDetailAsync(response, "Could not remove that cover", cancellationToken);
    }

    /// <summary>
    /// A PUT that answers with the refreshed series — which is every write on this page
    /// bar the bulk POST and the two that carry no JSON body.
    /// </summary>
    private async Task<SeriesDetailResponse?> WriteAsync<TRequest>(
        string route,
        TRequest request,
        string refusal,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.PutAsJsonAsync(route, request, cancellationToken);

        return await ReadDetailAsync(response, refusal, cancellationToken);
    }

    private static async Task<SeriesDetailResponse?> ReadDetailAsync(
        HttpResponseMessage response,
        string refusal,
        CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw Refused(refusal, response);
        }

        return await response.Content.ReadFromJsonAsync<SeriesDetailResponse>(cancellationToken);
    }

    private static HttpRequestException Refused(string what, HttpResponseMessage response) =>
        new($"{what}: {(int)response.StatusCode} {response.StatusCode}.");
}
