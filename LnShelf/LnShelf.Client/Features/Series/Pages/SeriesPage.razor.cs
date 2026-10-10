using LnShelf.Client.Features.Sync;

namespace LnShelf.Client.Features.Series.Pages;

public partial class SeriesPage : IDisposable
{
    [Parameter]
    public Guid SeriesId { get; set; }

    [Inject]
    public required SeriesState SeriesState { get; set; }

    /// <summary>
    /// Menu and select mode. Subscribed to here rather than in the volume section: only
    /// the page can call StateHasChanged for the whole subtree that reads it.
    /// </summary>
    [Inject]
    public required VolumeSelectionState Selection { get; set; }

    [Inject]
    public required SyncState SyncState { get; set; }

    /// <summary>
    /// Held here rather than in the volume section: the global volume view has to be
    /// read before the section first draws, or a series following the default would
    /// render as tiles and then swap to a list a moment later.
    /// </summary>
    [Inject]
    public required PreferencesState Preferences { get; set; }

    [Inject]
    public required IJSRuntime JavaScript { get; set; }

    [Inject]
    public required NavigationManager Navigation { get; set; }

    private SeriesDetailResponse? Series => SeriesState.Series;

    /// <summary>The token of the load in flight, cancelled when a newer one starts.</summary>
    private CancellationTokenSource? _loadCancellation;

    /// <summary>The series the volume selection belongs to.</summary>
    private Guid _selectionSeriesId;

    /// <summary>Whether the phone's More sheet is open.</summary>
    private bool _isActionsOpen;

    private ElementReference _moreButton;

    /// <summary>
    /// True until the series behind *this* route is the one in hand.
    /// </summary>
    // The id comparison is the half that matters: the state container is scoped, which
    // in a WASM app means it survives the navigation with the previous series still in
    // it, and "Series is null" is only true on the very first page load of a session.
    private bool IsLoading =>
        SeriesState.IsLoading || Series is null || Series.SeriesId != SeriesId;

    /// <summary>
    /// What pressing the button actually destroys, counted from this series.
    /// </summary>
    // Printed under the control rather than only inside the prompt: design.md forbids
    // hiding information behind an interaction, and the cost of this one is the whole
    // reason to hesitate.
    private string DeleteCost
    {
        get
        {
            if (Series is not { } series)
            {
                return string.Empty;
            }

            var parts = new List<string> { $"{series.Counts.Tracked} volumes" };

            if (series.Counts.Read > 0)
            {
                parts.Add($"{series.Counts.Read} read");
            }

            if (series.Counts.Owned > 0)
            {
                parts.Add($"{series.Counts.Owned} owned");
            }

            if (series.Spend is { } spend)
            {
                parts.Add($"{spend.Total.ToString("0", CultureInfo.InvariantCulture)} kr recorded");
            }

            return $"Deletes {string.Join(", ", parts)}. This cannot be undone.";
        }
    }

    /// <summary>What the last run actually did, or that there has not been one.</summary>
    // The mockup's line is "Volume list and dates refreshed nightly from AniList. Last
    // run 04:12, Vol 15 moved from 5 Sep to 12 Sep." The dates half of that cannot be
    // written — no source publishes them — so this says what is true instead.
    private string SyncLine => SyncState.Latest is { } latest
        ? $"Last run {latest.SyncedAtLabel} — {latest.Summary}"
        : "Not synced yet.";

    /// <summary>
    /// Takes a source's word for a disagreement, then refreshes the sidebar: its "to review"
    /// count is worked out from the choices that stand, so it moves with this one.
    /// </summary>
    private async Task OnChooseSourceAsync(SourceConflictResponse conflict, MetadataSourceName source)
    {
        await SeriesState.ChooseSourceAsync(conflict, source);
        await SyncState.LoadAsync();
    }

    /// <summary>Hands the disagreement back to the usual rules; the sidebar's count comes back with it.</summary>
    private async Task OnResetSourceAsync(SourceConflictResponse conflict)
    {
        await SeriesState.ResetSourceAsync(conflict);
        await SyncState.LoadAsync();
    }

    private void OpenActions() => _isActionsOpen = true;

    /// <summary>Closes the More sheet and hands focus back to the button that opened it.</summary>
    private async Task CloseActionsAsync()
    {
        _isActionsOpen = false;
        StateHasChanged();
        await _moreButton.FocusAsync();
    }

    /// <summary>The sheet's "Remove from shelf…": out of the sheet first, so the prompt is not behind it.</summary>
    private async Task OnDeleteFromSheetAsync()
    {
        _isActionsOpen = false;
        StateHasChanged();
        await OnDeleteAsync();
    }

    protected override void OnInitialized()
    {
        SeriesState.Changed += OnStateChanged;
        Selection.Changed += OnStateChanged;
        SyncState.Changed += OnStateChanged;
        Preferences.Changed += OnStateChanged;
    }

    /// <summary>
    /// Re-fetches on every parameter set, not just on first render or a changed id, so
    /// the page never shows the scoped container's old copy as if it were current.
    /// </summary>
    // Loading only when the id differed kept the container's copy across a delete and
    // Back, an import or a nightly sync. Each load gets its own token and cancels the one
    // before, so hopping LN → manga → LN cannot let the first response land last and
    // paint the wrong series under the URL.
    protected override async Task OnParametersSetAsync()
    {
        if (!Preferences.HasLoaded)
        {
            await Preferences.LoadAsync();
        }

        // A selection is a set of this series' volume ids; carried into the next series it
        // would be counted by "Mark N read" and match nothing there.
        if (_selectionSeriesId != SeriesId)
        {
            _selectionSeriesId = SeriesId;
            Selection.Reset();
        }

        await LoadSeriesAsync();

        if (!SyncState.HasLoaded)
        {
            await SyncState.LoadAsync();
        }
    }

    private async Task LoadSeriesAsync()
    {
        if (_loadCancellation is { } previous)
        {
            await previous.CancelAsync();
            previous.Dispose();
        }

        var cancellation = new CancellationTokenSource();
        _loadCancellation = cancellation;

        try
        {
            await SeriesState.LoadAsync(SeriesId, cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // Superseded by a newer load, or the page went away; nothing is waiting for
            // this one. SeriesState already dropped its response.
        }
    }

    /// <summary>
    /// Confirms, deletes, and leaves for the library.
    /// </summary>
    // The prompt names the series and its numbers, because "Are you sure?" on its own is
    // a question nobody can answer wrongly on purpose. Navigation only happens on a real
    // success — a failed delete leaves the page where it is, with the reason on it.
    private async Task OnDeleteAsync()
    {
        if (Series is not { } series)
        {
            return;
        }

        var confirmed = await JavaScript.InvokeAsync<bool>(
            "lnshelf.confirm",
            $"Remove “{series.TitleEnglish}” from the shelf?\n\n{DeleteCost}");

        if (!confirmed || !await SeriesState.DeleteAsync())
        {
            return;
        }

        // forceLoad: false — the library page re-fetches on initialisation, so a client
        // navigation is enough and a full reload would throw away the shell.
        Navigation.NavigateTo("/", forceLoad: false);
    }

    public void Dispose()
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = null;

        SeriesState.Changed -= OnStateChanged;
        Selection.Changed -= OnStateChanged;
        SyncState.Changed -= OnStateChanged;
        Preferences.Changed -= OnStateChanged;
    }

    private void OnStateChanged() => InvokeAsync(StateHasChanged);
}
