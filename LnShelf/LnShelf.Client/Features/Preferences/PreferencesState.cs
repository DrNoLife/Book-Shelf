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

    /// <summary>
    /// Whether a volume's own title is printed in its caption where one exists.
    /// </summary>
    // Off by default: most volumes are named "Volume N", which is the number the caption
    // already prints.
    public bool ShowVolumeTitles { get; private set; }

    /// <summary>
    /// What the shelf opens on when this browser remembers no filters of its own; Behind until
    /// the row says otherwise.
    /// </summary>
    public IReadOnlyList<LibraryFilter> DefaultLibraryFilters { get; private set; } = LibraryFilterSet.Default;

    /// <summary>The library page's sections, in the reader's order.</summary>
    public IReadOnlyList<LibrarySection> LibrarySectionOrder { get; private set; } = LibrarySections.Default;

    /// <summary>The unit a newly added manga or web novel starts in; each series can switch on its own page.</summary>
    public ProgressUnit DefaultProgressUnit { get; private set; } = ProgressUnit.Volumes;

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
                ShowVolumeTitles = preferences.ShowVolumeTitles;
                DefaultProgressUnit = preferences.DefaultProgressUnit;

                // Normalised here as well: the page draws one section per entry, so an order
                // missing one would lose that section outright.
                DefaultLibraryFilters = LibraryFilterSet.Normalize(preferences.DefaultLibraryFilters);
                LibrarySectionOrder = LibrarySections.Normalize(preferences.LibrarySectionOrder);
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

    public Task SetDefaultProgressUnitAsync(ProgressUnit progressUnit, CancellationToken cancellationToken = default)
    {
        if (progressUnit == DefaultProgressUnit)
        {
            return Task.CompletedTask;
        }

        DefaultProgressUnit = progressUnit;
        Changed?.Invoke();

        return SaveAsync(new UpdatePreferencesRequest { DefaultProgressUnit = progressUnit }, cancellationToken);
    }

    public Task SetShowVolumeTitlesAsync(bool show, CancellationToken cancellationToken = default)
    {
        if (show == ShowVolumeTitles)
        {
            return Task.CompletedTask;
        }

        ShowVolumeTitles = show;
        Changed?.Invoke();

        return SaveAsync(new UpdatePreferencesRequest { ShowVolumeTitles = show }, cancellationToken);
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

    public Task SetDefaultLibraryFiltersAsync(
        IReadOnlyList<LibraryFilter> filters,
        CancellationToken cancellationToken = default)
    {
        var normalized = LibraryFilterSet.Normalize(filters);
        if (normalized.SequenceEqual(DefaultLibraryFilters))
        {
            return Task.CompletedTask;
        }

        DefaultLibraryFilters = normalized;
        Changed?.Invoke();

        return SaveAsync(new UpdatePreferencesRequest { DefaultLibraryFilters = normalized }, cancellationToken);
    }

    public Task SetLibrarySectionOrderAsync(
        IReadOnlyList<LibrarySection> order,
        CancellationToken cancellationToken = default)
    {
        var normalized = LibrarySections.Normalize(order);
        if (normalized.SequenceEqual(LibrarySectionOrder))
        {
            return Task.CompletedTask;
        }

        LibrarySectionOrder = normalized;
        Changed?.Invoke();

        return SaveAsync(new UpdatePreferencesRequest { LibrarySectionOrder = normalized }, cancellationToken);
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
