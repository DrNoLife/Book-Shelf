namespace LnShelf.Client.Features.Series.Components;

public partial class ReadDatePanel
{
    /// <summary>The selected volumes that are read — the ones the date will be written to.</summary>
    [Parameter]
    public required IReadOnlyList<VolumeResponse> Volumes { get; set; }

    /// <summary>How many selected volumes are not read, and so are left out.</summary>
    [Parameter]
    public int SkippedCount { get; set; }

    /// <summary>Raised after a successful apply, and by Cancel.</summary>
    [Parameter]
    public EventCallback Closed { get; set; }

    [Inject]
    public required SeriesState SeriesState { get; set; }

    private string _text = string.Empty;

    private bool CanApply => !SeriesState.IsSaving && ReadDateInput.Parse(_text) is not null;

    private string ScopeLine
    {
        get
        {
            var applies = Volumes.Count == 1 ? "Applies to 1 read volume." : $"Applies to {Volumes.Count} read volumes.";

            return SkippedCount switch
            {
                0 => applies,
                1 => $"{applies} 1 selected volume is not read and is left out.",
                _ => $"{applies} {SkippedCount} selected volumes are not read and are left out.",
            };
        }
    }

    private async Task ApplyAsync()
    {
        if (ReadDateInput.Parse(_text) is not { } date)
        {
            return;
        }

        // Kept open on a failure: the message is printed by the page, and the reader's
        // date is still in the input to try again.
        if (await SeriesState.SetReadDatesAsync(Volumes, date))
        {
            await Closed.InvokeAsync();
        }
    }

    private Task CancelAsync() => Closed.InvokeAsync();
}
