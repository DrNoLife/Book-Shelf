using System.Globalization;
using LnShelf.Client.Features.Preferences;
using LnShelf.Client.Features.Sync;
using LnShelf.Shared.Metadata;
using LnShelf.Shared.Shelf;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace LnShelf.Client.Features.Series.Pages;

public partial class SeriesPage : IDisposable
{
    [Parameter]
    public Guid SeriesId { get; set; }

    [Inject]
    public required SeriesState SeriesState { get; set; }

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

    /// <summary>
    /// What the last run actually did, plus anything about this series it left for a
    /// person.
    /// </summary>
    // The mockup's line is "Volume list and dates refreshed nightly from AniList. Last
    // run 04:12, Vol 15 moved from 5 Sep to 12 Sep." The dates half of that cannot be
    // written — no source publishes them — so this says what is true instead.
    private IReadOnlyList<string> SyncLines
    {
        get
        {
            if (SyncState.Latest is not { } latest)
            {
                return ["Not synced yet."];
            }

            var lines = new List<string> { $"Last run {latest.SyncedAtLabel} — {latest.Summary}" };

            var unresolved = latest.Discrepancies
                .Where(discrepancy => discrepancy.SeriesId == SeriesId && discrepancy.NeedsReview)
                .ToList();

            foreach (var discrepancy in unresolved)
            {
                // The disagreement's own sentence, whichever sources are in it — the
                // shelf now reads three, and this line used to name exactly two.
                lines.Add(discrepancy.Field is SyncDiscrepancyField.SeriesLink
                    ? "Not linked to a release source yet — waiting on you."
                    : $"{Where(discrepancy)}{discrepancy.Field}: {discrepancy.Disagreement}.");
            }

            return lines;
        }
    }

    /// <summary>
    /// "Vol 15 — " when the disagreement is about one volume, nothing when it is about
    /// the series.
    /// </summary>
    // Per-volume dates made this necessary: "ReleaseDate: MangaUpdates says in print,
    // OpenLibrary says 2026-12-05" is unreadable without knowing which volume it is
    // about, and every date disagreement is about exactly one.
    private static string Where(SyncDiscrepancyResponse discrepancy) =>
        discrepancy.VolumeNumber is { } volumeNumber ? $"Vol {volumeNumber} — " : string.Empty;

    protected override void OnInitialized()
    {
        SeriesState.Changed += OnStateChanged;
        SyncState.Changed += OnStateChanged;
        Preferences.Changed += OnStateChanged;
    }

    /// <summary>
    /// Re-fetches on parameter change, not just on first render, so navigating from one
    /// series to another actually loads the second one.
    /// </summary>
    protected override async Task OnParametersSetAsync()
    {
        if (!Preferences.HasLoaded)
        {
            await Preferences.LoadAsync();
        }

        if (Series?.SeriesId != SeriesId)
        {
            await SeriesState.LoadAsync(SeriesId);
        }

        if (!SyncState.HasLoaded)
        {
            await SyncState.LoadAsync();
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
        SeriesState.Changed -= OnStateChanged;
        SyncState.Changed -= OnStateChanged;
        Preferences.Changed -= OnStateChanged;
    }

    private void OnStateChanged() => InvokeAsync(StateHasChanged);
}
