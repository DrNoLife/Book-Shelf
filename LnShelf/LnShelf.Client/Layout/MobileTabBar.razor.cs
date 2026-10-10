using Microsoft.AspNetCore.Components.Routing;

namespace LnShelf.Client.Layout;

public partial class MobileTabBar : IDisposable
{
    [Inject]
    public required NavigationManager Navigation { get; set; }

    /// <summary>The five tabs, left to right, in the mockup's order.</summary>
    private static readonly IReadOnlyList<Tab> Tabs =
    [
        new("", "Library", IconName.Books),
        new("upcoming", "Upcoming", IconName.CalendarBlank),
        new("search", "Search and add", IconName.Plus, IsAdd: true),
        new("stats", "Stats", IconName.ChartBar),
        new("profile", "Profile", IconName.User),
    ];

    /// <summary>The route without its query or fragment: "", "upcoming", "series/…".</summary>
    private string CurrentPath
    {
        get
        {
            var relative = Navigation.ToBaseRelativePath(Navigation.Uri);
            var end = relative.IndexOfAny(['?', '#']);
            return end < 0 ? relative : relative[..end];
        }
    }

    private bool IsHidden => CurrentPath.StartsWith("series/", StringComparison.OrdinalIgnoreCase);

    // The library is the empty path only; every other tab owns its route and anything under it.
    private bool IsCurrent(Tab tab) => tab.Href.Length == 0
        ? CurrentPath.Length == 0
        : CurrentPath.Equals(tab.Href, StringComparison.OrdinalIgnoreCase)
          || CurrentPath.StartsWith($"{tab.Href}/", StringComparison.OrdinalIgnoreCase);

    protected override void OnInitialized() => Navigation.LocationChanged += OnLocationChanged;

    public void Dispose() => Navigation.LocationChanged -= OnLocationChanged;

    private void OnLocationChanged(object? sender, LocationChangedEventArgs args) => InvokeAsync(StateHasChanged);

    private sealed record Tab(string Href, string Label, IconName Icon, bool IsAdd = false);
}
