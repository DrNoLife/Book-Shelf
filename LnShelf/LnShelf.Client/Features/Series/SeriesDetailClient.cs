using System.Net;
using LnShelf.Shared.Notifications;

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

    /// <summary>
    /// What each source said about every disagreement on this series, laid out side by side.
    /// </summary>
    public Task<IReadOnlyList<SourceConflictResponse>?> GetConflictsAsync(
        Guid seriesId,
        CancellationToken cancellationToken = default) =>
        httpClient.GetFromJsonAsync<IReadOnlyList<SourceConflictResponse>>(
            $"api/series/{seriesId}/conflicts", cancellationToken);

    /// <summary>Picks the source that decides one field. Answers with the refreshed series.</summary>
    public Task<SeriesDetailResponse?> ChooseSourceAsync(
        Guid seriesId,
        SyncDiscrepancyField field,
        decimal? volumeNumber,
        MetadataSourceName source,
        CancellationToken cancellationToken = default) =>
        WriteAsync(
            $"api/series/{seriesId}/source-choice",
            new ChooseSourceRequest { Field = field, VolumeNumber = volumeNumber, Source = source },
            "Could not save that choice",
            cancellationToken);

    /// <summary>Hands a field back to the usual rules. Answers with the refreshed series.</summary>
    public async Task<SeriesDetailResponse?> ResetSourceAsync(
        Guid seriesId,
        SyncDiscrepancyField field,
        decimal? volumeNumber,
        CancellationToken cancellationToken = default)
    {
        var query = volumeNumber is { } number
            ? $"field={field}&volumeNumber={number.ToVolumeText()}"
            : $"field={field}";

        using var response = await httpClient.DeleteAsync(
            $"api/series/{seriesId}/source-choice?{query}", cancellationToken);

        return await ReadDetailAsync(response, "Could not save that choice", cancellationToken);
    }

    public Task<SeriesDetailResponse?> SetReadAsync(
        Guid volumeId,
        ReadStatus status,
        CancellationToken cancellationToken = default) =>
        WriteAsync(
            $"api/volumes/{volumeId}/read",
            new SetVolumeReadRequest { Status = status },
            "Could not save that",
            cancellationToken);

    /// <summary>Sets when a read volume was read, or clears the date with null.</summary>
    public Task<SeriesDetailResponse?> SetReadDateAsync(
        Guid volumeId,
        DateOnly? readAt,
        CancellationToken cancellationToken = default) =>
        WriteAsync(
            $"api/volumes/{volumeId}/read-date",
            new SetVolumeReadDateRequest { ReadAt = readAt },
            "Could not save that date",
            cancellationToken);

    /// <summary>Sets when several read volumes of one series were read, or clears their dates.</summary>
    public Task<SeriesDetailResponse?> SetReadDatesAsync(
        Guid seriesId,
        IReadOnlyList<Guid> volumeIds,
        DateOnly? readAt,
        CancellationToken cancellationToken = default) =>
        WriteAsync(
            $"api/series/{seriesId}/volumes/read-date",
            new SetVolumeReadDatesRequest { VolumeIds = volumeIds, ReadAt = readAt },
            "Could not save those dates",
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
        decimal throughNumber,
        ReadStatus? read,
        bool? owned,
        OwnedFormat? ownedFormat = null,
        CancellationToken cancellationToken = default)
    {
        var request = new BulkVolumeStateRequest
        {
            ThroughNumber = throughNumber,
            Read = read,
            Owned = owned,
            Format = ownedFormat,
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

    /// <summary>Mutes or unmutes notifications about this series. Answers with the refreshed series.</summary>
    public Task<SeriesDetailResponse?> SetNotificationsMutedAsync(
        Guid seriesId,
        bool muted,
        CancellationToken cancellationToken = default) =>
        WriteAsync(
            $"api/series/{seriesId}/notifications",
            new SetSeriesNotificationsRequest { Muted = muted },
            "Could not save that",
            cancellationToken);

    /// <summary>Counts the series in volumes or in chapters. Answers with the refreshed series.</summary>
    public Task<SeriesDetailResponse?> SetProgressUnitAsync(
        Guid seriesId,
        ProgressUnit progressUnit,
        CancellationToken cancellationToken = default) =>
        WriteAsync(
            $"api/series/{seriesId}/progress-unit",
            new SetProgressUnitRequest { ProgressUnit = progressUnit },
            "Could not save that",
            cancellationToken);

    /// <summary>Records the last chapter read, or clears it with null. Answers with the refreshed series.</summary>
    public Task<SeriesDetailResponse?> SetLastChapterReadAsync(
        Guid seriesId,
        decimal? lastChapterRead,
        CancellationToken cancellationToken = default) =>
        WriteAsync(
            $"api/series/{seriesId}/chapter",
            new SetLastChapterReadRequest { LastChapterRead = lastChapterRead },
            "Could not save that chapter",
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
    /// Adds a volume to a series kept by hand: the next whole number when none is given.
    /// </summary>
    public async Task<SeriesDetailResponse?> AddVolumeAsync(
        Guid seriesId,
        decimal? number,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(
            $"api/series/{seriesId}/volumes", new AddLocalVolumeRequest { Number = number }, cancellationToken);

        return await ReadDetailAsync(response, "Could not add that volume", cancellationToken);
    }

    /// <summary>Removes a volume added by hand, with the reader's state on it.</summary>
    public async Task<SeriesDetailResponse?> RemoveVolumeAsync(
        Guid seriesId,
        Guid volumeId,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.DeleteAsync(
            $"api/series/{seriesId}/volumes/{volumeId}", cancellationToken);

        return await ReadDetailAsync(response, "Could not remove that volume", cancellationToken);
    }

    /// <summary>"More by {author}": AniList's list, marked with what is already on the shelf.</summary>
    public Task<AuthorWorksResponse?> GetMoreByAuthorAsync(
        Guid seriesId,
        CancellationToken cancellationToken = default) =>
        httpClient.GetFromJsonAsync<AuthorWorksResponse>($"api/series/{seriesId}/more-by-author", cancellationToken);

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
