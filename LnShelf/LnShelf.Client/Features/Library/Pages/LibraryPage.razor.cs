using LnShelf.Client.Features.Profile;

namespace LnShelf.Client.Features.Library.Pages;

public partial class LibraryPage : IDisposable
{
    [Inject]
    public required LibraryState LibraryState { get; set; }

    [Inject]
    public required PreferencesState Preferences { get; set; }

    [Inject]
    public required FinishReadingState Finishing { get; set; }

    /// <summary>Only for the phone header's avatar.</summary>
    [Inject]
    public required ProfileState Profile { get; set; }

    private LibraryResponse? Library => LibraryState.Library;

    // Both lists follow the format tab but not the status filter or the text box: they
    // are about the whole shelf, not about whatever the grid below is narrowed to.
    private IReadOnlyList<ContinueReadingEntry> ContinueEntries =>
        LibraryState.Overview is { } overview ? LibraryHighlights.ContinueReading(overview.Cards, take: 3) : [];

    private IReadOnlyList<OutNowEntry> OutNowEntries =>
        LibraryState.Overview is { } overview ? LibraryHighlights.OutNow(overview.Cards, take: 8) : [];

    /// <summary>
    /// The phone's carousel: more than the desktop's three, because a swipe costs nothing and
    /// a row of three cards on a phone is one and a half screens.
    /// </summary>
    private IReadOnlyList<ContinueReadingEntry> UpNextEntries =>
        LibraryState.Overview is { } overview ? LibraryHighlights.ContinueReading(overview.Cards, take: 12) : [];

    /// <summary>Every series behind on buying: the phone list prints three and counts the rest.</summary>
    private IReadOnlyList<OutNowEntry> AllOutNowEntries =>
        LibraryState.Overview is { } overview ? LibraryHighlights.OutNow(overview.Cards, take: overview.Cards.Count) : [];

    protected override async Task OnInitializedAsync()
    {
        LibraryState.Changed += OnStateChanged;
        Preferences.Changed += OnStateChanged;
        Finishing.Changed += OnStateChanged;
        Profile.Changed += OnStateChanged;

        // The density, sort, section order and default filters come from the server row, so
        // they are read before the grid is drawn — a grid that re-flows a moment after landing
        // reads as a bug. Guarded, as the series page's is: the container is scoped, so a
        // second visit to the library already has them.
        if (!Preferences.HasLoaded)
        {
            await Preferences.LoadAsync();
        }
        LibraryState.SetSort(Preferences.LibrarySort);

        // After the preferences, which hold the default it falls back to.
        await LibraryState.RestoreFiltersAsync();
        await LibraryState.LoadAsync();

        // After the shelf, never before it: the avatar is decoration, the shelf is not.
        await Profile.LoadUserAsync();
    }

    public void Dispose()
    {
        LibraryState.Changed -= OnStateChanged;
        Preferences.Changed -= OnStateChanged;
        Finishing.Changed -= OnStateChanged;
        Profile.Changed -= OnStateChanged;
    }

    private void OnStateChanged() => InvokeAsync(StateHasChanged);

    private Task OnFinishedAsync(ContinueReadingEntry entry) => Finishing.FinishAsync(entry);
}
