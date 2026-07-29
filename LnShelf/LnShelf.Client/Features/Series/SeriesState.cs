using System.Net.Http.Json;
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

    public void ToggleSelectMode()
    {
        IsSelectMode = !IsSelectMode;
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
