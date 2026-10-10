using Microsoft.AspNetCore.Components.Routing;

namespace LnShelf.Client;

/// <summary>
/// The pages visited in this session, newest last, so a page can send the reader back to
/// where they came from — the search page's "Cancel" — rather than to a fixed place.
/// </summary>
// Recorded here rather than read from the browser's history: history.back() cannot tell an
// in-app page from the site the reader arrived from, and a Cancel that leaves the app is
// worse than one that lands on the library.
public sealed class NavigationTrail : IDisposable
{
    // Enough to step over a few visits to the same page; nothing reads further back.
    private const int Depth = 20;

    private readonly NavigationManager _navigation;
    private readonly List<string> _locations = [];

    public NavigationTrail(NavigationManager navigation)
    {
        _navigation = navigation;
        _locations.Add(navigation.Uri);
        navigation.LocationChanged += OnLocationChanged;
    }

    /// <summary>
    /// The most recent location that is not the page at <paramref name="relativePath"/> —
    /// "search" for the search page. Null when the reader has been nowhere else.
    /// </summary>
    public string? LastLocationOutside(string relativePath) =>
        _locations.LastOrDefault(location => PathOf(location) != relativePath.Trim('/'));

    public void Dispose() => _navigation.LocationChanged -= OnLocationChanged;

    private void OnLocationChanged(object? sender, LocationChangedEventArgs args)
    {
        _locations.Add(args.Location);

        if (_locations.Count > Depth)
        {
            _locations.RemoveAt(0);
        }
    }

    private string PathOf(string location)
    {
        var relative = _navigation.ToBaseRelativePath(location);
        var end = relative.IndexOfAny(['?', '#']);

        return (end < 0 ? relative : relative[..end]).Trim('/');
    }
}
