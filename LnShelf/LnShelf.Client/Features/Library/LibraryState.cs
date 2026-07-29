using System.Net.Http.Json;
using LnShelf.Shared.Shelf;

namespace LnShelf.Client.Features.Library;

/// <summary>
/// The library view's state container.
/// </summary>
// The named seam: the grid and the sidebar both render from one fetch instead of
// issuing their own requests, which is what lets the sidebar show counts across
// the whole format scope while the grid shows only the filtered subset.
public sealed class LibraryState(HttpClient httpClient)
{
    /// <summary>Null is the "All" tab.</summary>
    public Format? SelectedFormat { get; private set; }

    /// <summary>Behind is the opinionated default (feature-spec §4.6).</summary>
    public LibraryFilter SelectedFilter { get; private set; } = LibraryFilter.Behind;

    public LibraryResponse? Library { get; private set; }
    public bool IsLoading { get; private set; }
    public bool LoadFailed { get; private set; }

    public event Action? Changed;

    public Task LoadAsync(CancellationToken cancellationToken = default) =>
        ReloadAsync(cancellationToken);

    public async Task SelectFormatAsync(Format? format, CancellationToken cancellationToken = default)
    {
        if (SelectedFormat == format)
        {
            return;
        }

        SelectedFormat = format;
        await ReloadAsync(cancellationToken);
    }

    public async Task SelectFilterAsync(LibraryFilter filter, CancellationToken cancellationToken = default)
    {
        if (SelectedFilter == filter)
        {
            return;
        }

        SelectedFilter = filter;
        await ReloadAsync(cancellationToken);
    }

    private async Task ReloadAsync(CancellationToken cancellationToken)
    {
        IsLoading = true;
        LoadFailed = false;
        Changed?.Invoke();

        try
        {
            var route = SelectedFormat is null
                ? $"api/library?filter={SelectedFilter}"
                : $"api/library?filter={SelectedFilter}&format={SelectedFormat}";

            Library = await httpClient.GetFromJsonAsync<LibraryResponse>(route, cancellationToken);
        }
        catch (HttpRequestException)
        {
            // Rendering "could not reach the API" is handling it; there is
            // nothing else the client can do about a dead API.
            LoadFailed = true;
        }
        finally
        {
            IsLoading = false;
            Changed?.Invoke();
        }
    }
}
