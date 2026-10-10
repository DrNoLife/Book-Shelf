using LnShelf.Shared.Changelog;

namespace LnShelf.Client.Features.Changelog.Pages;

public partial class ChangelogPage : IDisposable
{
    [Inject]
    public required ChangelogState State { get; set; }

    protected override async Task OnInitializedAsync()
    {
        State.Changed += OnStateChanged;
        await State.LoadAsync();
    }

    public void Dispose() => State.Changed -= OnStateChanged;

    // The anchor is what the version index links to; the heading id names the release for
    // a screen reader through aria-labelledby.
    private static string AnchorId(ChangelogEntryResponse entry) => $"v{entry.Version}";

    private static string HeadingId(ChangelogEntryResponse entry) => $"release-{entry.Version}";

    private void OnStateChanged() => InvokeAsync(StateHasChanged);
}
