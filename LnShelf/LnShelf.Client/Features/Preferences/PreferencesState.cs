using System.Net.Http.Json;
using LnShelf.Shared.Preferences;

namespace LnShelf.Client.Features.Preferences;

/// <summary>
/// The single preferences row, held once for the whole shell.
/// </summary>
// Optimistic: a toggle applies locally and then persists, because waiting on a round
// trip to redraw a grid the user just asked to redraw feels broken. A failed save
// leaves the local value in place and says so — the next load corrects it.
public sealed class PreferencesState(HttpClient httpClient)
{
    public GridDensity GridDensity { get; private set; } = GridDensity.Comfortable;
    public LibrarySort LibrarySort { get; private set; } = LibrarySort.MostBehind;
    public LibraryView LibraryView { get; private set; } = LibraryView.Covers;

    /// <summary>The series-detail default; a series can override it for itself.</summary>
    public VolumeView VolumeView { get; private set; } = VolumeView.Tiles;

    public bool HasLoaded { get; private set; }
    public string? Message { get; private set; }

    public event Action? Changed;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var preferences = await httpClient.GetFromJsonAsync<PreferencesResponse>(
                "api/preferences", cancellationToken);

            if (preferences is not null)
            {
                GridDensity = preferences.GridDensity;
                LibrarySort = preferences.LibrarySort;
                LibraryView = preferences.LibraryView;
                VolumeView = preferences.VolumeView;
            }
        }
        catch (HttpRequestException)
        {
            // The defaults are already loaded; a dead API is not a reason to have no
            // preferences at all.
        }
        finally
        {
            HasLoaded = true;
            Changed?.Invoke();
        }
    }

    public Task SetDensityAsync(GridDensity density, CancellationToken cancellationToken = default)
    {
        if (density == GridDensity)
        {
            return Task.CompletedTask;
        }

        GridDensity = density;
        Changed?.Invoke();

        return SaveAsync(new UpdatePreferencesRequest { GridDensity = density }, cancellationToken);
    }

    public Task SetLibraryViewAsync(LibraryView view, CancellationToken cancellationToken = default)
    {
        if (view == LibraryView)
        {
            return Task.CompletedTask;
        }

        LibraryView = view;
        Changed?.Invoke();

        return SaveAsync(new UpdatePreferencesRequest { LibraryView = view }, cancellationToken);
    }

    public Task SetVolumeViewAsync(VolumeView view, CancellationToken cancellationToken = default)
    {
        if (view == VolumeView)
        {
            return Task.CompletedTask;
        }

        VolumeView = view;
        Changed?.Invoke();

        return SaveAsync(new UpdatePreferencesRequest { VolumeView = view }, cancellationToken);
    }

    public Task SetSortAsync(LibrarySort sort, CancellationToken cancellationToken = default)
    {
        if (sort == LibrarySort)
        {
            return Task.CompletedTask;
        }

        LibrarySort = sort;
        Changed?.Invoke();

        return SaveAsync(new UpdatePreferencesRequest { LibrarySort = sort }, cancellationToken);
    }

    private async Task SaveAsync(UpdatePreferencesRequest request, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.PutAsJsonAsync("api/preferences", request, cancellationToken);

            Message = response.IsSuccessStatusCode
                ? null
                : "That preference did not save; it will revert next time the page loads.";
        }
        catch (HttpRequestException exception)
        {
            Message = $"That preference did not save: {exception.Message}";
        }

        Changed?.Invoke();
    }
}
