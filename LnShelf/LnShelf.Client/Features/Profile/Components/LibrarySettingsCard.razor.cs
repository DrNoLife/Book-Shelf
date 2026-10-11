namespace LnShelf.Client.Features.Profile.Components;

public partial class LibrarySettingsCard : IDisposable
{
    [Inject]
    public required PreferencesState Preferences { get; set; }

    private IReadOnlyList<LibrarySection> Order => Preferences.LibrarySectionOrder;

    protected override async Task OnInitializedAsync()
    {
        Preferences.Changed += OnStateChanged;

        // The profile can be the first page of a session, before the library has read the row.
        if (!Preferences.HasLoaded)
        {
            await Preferences.LoadAsync();
        }
    }

    public void Dispose() => Preferences.Changed -= OnStateChanged;

    private void OnStateChanged() => InvokeAsync(StateHasChanged);

    private Task OnDefaultFiltersChangedAsync(IReadOnlyList<LibraryFilter> filters) =>
        Preferences.SetDefaultLibraryFiltersAsync(filters);

    private Task MoveAsync(LibrarySection section, int offset) =>
        Preferences.SetLibrarySectionOrderAsync(LibrarySectionMoves.Move(Order, section, offset));
}
