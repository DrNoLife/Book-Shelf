using System.Net.Http.Json;
using LnShelf.Shared.Preferences;
using LnShelf.Shared.Shelf;

namespace LnShelf.Client.Features.Series;

/// <summary>
/// The series-detail page's state: one fetch, then per-volume writes that update it
/// in place.
/// </summary>
public sealed class SeriesState(HttpClient httpClient)
{
    public SeriesDetailResponse? Series { get; private set; }
    public bool IsLoading { get; private set; }
    public bool LoadFailed { get; private set; }

    /// <summary>True while a write is in flight — the tiles go inert.</summary>
    public bool IsSaving { get; private set; }

    public string? Message { get; private set; }

    /// <summary>
    /// Incremented on every cover write. The override object key never changes, so
    /// this is what tells the browser the bytes behind it did.
    /// </summary>
    public int CoverVersion { get; private set; }

    /// <summary>
    /// The volume whose state menu is open, or null. Held here rather than on a tile
    /// so only one can be open, and so opening one closes the last.
    /// </summary>
    public Guid? OpenMenuVolumeId { get; private set; }

    /// <summary>Select mode and its selection, which live with the page, not a tile.</summary>
    public bool IsSelectMode { get; private set; }

    public IReadOnlySet<Guid> SelectedVolumeIds => _selected;

    private readonly HashSet<Guid> _selected = [];

    public event Action? Changed;

    public async Task LoadAsync(Guid seriesId, CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        LoadFailed = false;
        Message = null;
        Changed?.Invoke();

        try
        {
            Series = await httpClient.GetFromJsonAsync<SeriesDetailResponse>(
                $"api/series/{seriesId}", cancellationToken);
        }
        catch (HttpRequestException)
        {
            LoadFailed = true;
        }
        finally
        {
            IsLoading = false;
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Unread → Reading → Read → Unread. The cycle lives here rather than on the
    /// server, so the endpoint stays an idempotent "set this status".
    /// </summary>
    public static ReadStatus NextInCycle(ReadStatus current) => current switch
    {
        ReadStatus.Unread => ReadStatus.Reading,
        ReadStatus.Reading => ReadStatus.Read,
        _ => ReadStatus.Unread,
    };

    public Task CycleReadAsync(VolumeResponse volume, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(volume);

        return WriteVolumeAsync(
            $"api/volumes/{volume.VolumeId}/read",
            new SetVolumeReadRequest { Status = NextInCycle(volume.ReadStatus) },
            cancellationToken);
    }

    public Task ToggleOwnedAsync(VolumeResponse volume, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(volume);

        return WriteVolumeAsync(
            $"api/volumes/{volume.VolumeId}/owned",
            new SetVolumeOwnedRequest { Owned = !volume.Owned },
            cancellationToken);
    }

    /// <summary>"Mark 1–N read" / "Mark 1–N owned".</summary>
    public async Task MarkThroughAsync(
        int throughNumber,
        ReadStatus? read,
        bool? owned,
        CancellationToken cancellationToken = default)
    {
        if (Series is null)
        {
            return;
        }

        IsSaving = true;
        Message = null;
        Changed?.Invoke();

        try
        {
            var request = new BulkVolumeStateRequest
            {
                ThroughNumber = throughNumber,
                Read = read,
                Owned = owned,
            };

            using var response = await httpClient.PostAsJsonAsync(
                $"api/series/{Series.SeriesId}/volumes/bulk", request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                // The bulk endpoint answers with the whole series, so one response
                // refreshes the hero's counts and every tile at once.
                Series = await response.Content.ReadFromJsonAsync<SeriesDetailResponse>(cancellationToken);
            }
            else
            {
                Message = $"Could not apply that: {(int)response.StatusCode} {response.StatusCode}.";
            }
        }
        catch (HttpRequestException exception)
        {
            Message = exception.Message;
        }
        finally
        {
            IsSaving = false;
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Sets this series' volume view, or clears the override with null so it follows the
    /// global preference again.
    /// </summary>
    // The endpoint answers with the whole series, so the page redraws from one response
    // — the same shape as the bulk write above.
    public async Task SetVolumeViewOverrideAsync(
        VolumeView? view,
        CancellationToken cancellationToken = default)
    {
        if (Series is null)
        {
            return;
        }

        IsSaving = true;
        Message = null;
        Changed?.Invoke();

        try
        {
            using var response = await httpClient.PutAsJsonAsync(
                $"api/series/{Series.SeriesId}/volume-view",
                new SetSeriesVolumeViewRequest { View = view },
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                Series = await response.Content.ReadFromJsonAsync<SeriesDetailResponse>(cancellationToken);
            }
            else
            {
                Message = $"Could not save that view: {(int)response.StatusCode} {response.StatusCode}.";
            }
        }
        catch (HttpRequestException exception)
        {
            Message = exception.Message;
        }
        finally
        {
            IsSaving = false;
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Removes the loaded series from the shelf. The caller confirms first.
    /// </summary>
    // No optimistic update and no local removal: the page this state belongs to is about
    // to be navigated away from, so the honest thing is to report whether the server
    // actually did it and let the caller leave.
    public async Task<bool> DeleteAsync(CancellationToken cancellationToken = default)
    {
        if (Series is not { } series)
        {
            return false;
        }

        IsSaving = true;
        Message = null;
        Changed?.Invoke();

        try
        {
            using var response = await httpClient.DeleteAsync(
                $"api/series/{series.SeriesId}", cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            Message = response.StatusCode is System.Net.HttpStatusCode.NotFound
                ? "That series is not on the shelf any more."
                : $"Could not delete that series: {(int)response.StatusCode} {response.StatusCode}.";

            return false;
        }
        catch (HttpRequestException exception)
        {
            Message = exception.Message;
            return false;
        }
        finally
        {
            IsSaving = false;
            Changed?.Invoke();
        }
    }

    public void OpenMenu(VolumeResponse volume)
    {
        ArgumentNullException.ThrowIfNull(volume);

        OpenMenuVolumeId = volume.VolumeId;
        Changed?.Invoke();
    }

    public void CloseMenu()
    {
        if (OpenMenuVolumeId is null)
        {
            return;
        }

        OpenMenuVolumeId = null;
        Changed?.Invoke();
    }

    /// <summary>Sets a read status outright — what the menu does, as against the cycle.</summary>
    public Task SetReadAsync(
        VolumeResponse volume,
        ReadStatus status,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(volume);

        return WriteVolumeAsync(
            $"api/volumes/{volume.VolumeId}/read",
            new SetVolumeReadRequest { Status = status },
            cancellationToken);
    }

    /// <summary>
    /// Sets ownership outright, optionally with the edition. Passing a format on an
    /// owned volume is how the menu switches physical to digital without a second call.
    /// </summary>
    public Task SetOwnedAsync(
        VolumeResponse volume,
        bool owned,
        OwnedFormat? format = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(volume);

        return WriteVolumeAsync(
            $"api/volumes/{volume.VolumeId}/owned",
            new SetVolumeOwnedRequest { Owned = owned, Format = format },
            cancellationToken);
    }

    /// <summary>
    /// Uploads a cover override. Multipart rather than JSON because the payload is
    /// image bytes, and the endpoint caps the size (feature-spec §4.3).
    /// </summary>
    public async Task SetCoverAsync(
        VolumeResponse volume,
        Stream content,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(volume);
        ArgumentNullException.ThrowIfNull(content);

        if (Series is null)
        {
            return;
        }

        IsSaving = true;
        Message = null;
        Changed?.Invoke();

        try
        {
            using var form = new MultipartFormDataContent();
            using var streamContent = new StreamContent(content);
            form.Add(streamContent, "file", fileName);

            using var response = await httpClient.PutAsync(
                $"api/volumes/{volume.VolumeId}/cover", form, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                Message = response.StatusCode is System.Net.HttpStatusCode.BadRequest
                    ? "That file is not an image the shelf can read."
                    : $"Could not set that cover: {(int)response.StatusCode} {response.StatusCode}.";
                return;
            }

            var updated = await response.Content.ReadFromJsonAsync<VolumeResponse>(cancellationToken);
            if (updated is not null)
            {
                CoverVersion++;
                Series = Replace(Series, updated);
                Message = $"Cover set for volume {updated.Number}.";
            }
        }
        catch (HttpRequestException exception)
        {
            Message = exception.Message;
        }
        finally
        {
            IsSaving = false;
            Changed?.Invoke();
        }
    }

    public async Task RemoveCoverAsync(VolumeResponse volume, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(volume);

        if (Series is null)
        {
            return;
        }

        IsSaving = true;
        Message = null;
        Changed?.Invoke();

        try
        {
            using var response = await httpClient.DeleteAsync(
                $"api/volumes/{volume.VolumeId}/cover", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                Message = $"Could not remove that cover: {(int)response.StatusCode} {response.StatusCode}.";
                return;
            }

            var updated = await response.Content.ReadFromJsonAsync<VolumeResponse>(cancellationToken);
            if (updated is not null)
            {
                Series = Replace(Series, updated);
            }
        }
        catch (HttpRequestException exception)
        {
            Message = exception.Message;
        }
        finally
        {
            IsSaving = false;
            Changed?.Invoke();
        }
    }

    public void ToggleSelectMode()
    {
        IsSelectMode = !IsSelectMode;

        // A menu belonging to a tile that is about to become a checkbox has to go.
        OpenMenuVolumeId = null;

        if (!IsSelectMode)
        {
            _selected.Clear();
        }

        Changed?.Invoke();
    }

    public void ToggleSelection(VolumeResponse volume)
    {
        ArgumentNullException.ThrowIfNull(volume);

        if (!_selected.Remove(volume.VolumeId))
        {
            _selected.Add(volume.VolumeId);
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// Writes one volume and swaps the returned row into the loaded series.
    /// </summary>
    // The response carries the row the server actually stored, so the tile shows
    // what is in the database rather than what the click assumed.
    private async Task WriteVolumeAsync<TRequest>(
        string route,
        TRequest request,
        CancellationToken cancellationToken)
    {
        if (Series is null)
        {
            return;
        }

        IsSaving = true;
        Message = null;
        Changed?.Invoke();

        try
        {
            using var response = await httpClient.PutAsJsonAsync(route, request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                Message = $"Could not save that: {(int)response.StatusCode} {response.StatusCode}.";
                return;
            }

            var updated = await response.Content.ReadFromJsonAsync<VolumeResponse>(cancellationToken);
            if (updated is not null)
            {
                Series = Replace(Series, updated);
            }
        }
        catch (HttpRequestException exception)
        {
            Message = exception.Message;
        }
        finally
        {
            IsSaving = false;
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Swaps one volume in and recomputes the hero's counts from the volumes, so the
    /// numbers and the grid cannot disagree without a round trip.
    /// </summary>
    private static SeriesDetailResponse Replace(SeriesDetailResponse series, VolumeResponse updated)
    {
        var volumes = series.Volumes
            .Select(volume => volume.VolumeId == updated.VolumeId ? updated : volume)
            .ToList();

        return series with
        {
            Volumes = volumes,
            Counts = series.Counts with
            {
                Read = volumes.Count(volume => volume.ReadStatus is ReadStatus.Read),
                Owned = volumes.Count(volume => volume.Owned),
                Published = volumes.Count(volume => volume.IsOut),
                ToBuy = volumes.Count(volume => volume.IsOut && !volume.Owned),
                Backlog = volumes.Count(volume => volume.Owned && volume.ReadStatus is not ReadStatus.Read),
                NextToBuyNumber = volumes
                    .Where(volume => volume.IsOut && !volume.Owned)
                    .Select(volume => (int?)volume.Number)
                    .FirstOrDefault(),
                NotOut = volumes.Count(volume => !volume.IsOut),
                Dated = volumes.Count(volume => !volume.IsOut && volume.ReleaseDate is not null),
            },
        };
    }
}
