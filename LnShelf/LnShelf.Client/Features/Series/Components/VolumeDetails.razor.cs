namespace LnShelf.Client.Features.Series.Components;

public partial class VolumeDetails
{
    /// <summary>The single ownership choice: not owned, or owned in one edition.</summary>
    private enum Ownership
    {
        None,
        Physical,
        Digital,
    }

    [Parameter]
    public required VolumeResponse Volume { get; set; }

    /// <summary>The series' title, which the Saxo search is built from.</summary>
    [Parameter]
    public required string SeriesTitle { get; set; }

    [Parameter]
    public bool IsBusy { get; set; }

    /// <summary>
    /// Raised when a choice is complete in the popover's sense — the popover closes on it.
    /// The bottom sheet ignores it and stays open until Done, so the reader sees the change land.
    /// </summary>
    [Parameter]
    public EventCallback Settled { get; set; }

    /// <summary>Asks the owner to open its file picker for this volume.</summary>
    [Parameter]
    public EventCallback<VolumeResponse> CoverPickRequested { get; set; }

    [Inject]
    public required SeriesState SeriesState { get; set; }

    [Inject]
    public required IJSRuntime JavaScript { get; set; }

    /// <summary>What the date input holds right now, or null when it has not been touched.</summary>
    private string? _draft;

    private ElementReference _readDateInput;

    private bool _isCommitting;

    private string? _error;

    private Guid _volumeId;

    /// <summary>Out and not owned — the "To buy" state, the one that offers the link.</summary>
    internal static bool IsBuyable(VolumeResponse volume)
    {
        ArgumentNullException.ThrowIfNull(volume);
        return volume.IsOut && !volume.Owned;
    }

    private static readonly IReadOnlyList<SegmentedOption<ReadStatus>> ReadOptions =
    [
        new(ReadStatus.Unread, "Unread", "Unread"),
        new(ReadStatus.Reading, "Reading", "Reading"),
        new(ReadStatus.Read, "Read", "Read"),
    ];

    private static readonly IReadOnlyList<SegmentedOption<Ownership>> OwnershipOptions =
    [
        new(Ownership.None, "No", "Not owned"),
        new(Ownership.Physical, "Physical", "Owned, physical edition"),
        new(Ownership.Digital, "Digital", "Owned, digital edition"),
    ];

    // An owned volume with no recorded edition is physical: that is what the API writes
    // for it the moment it is marked owned without one.
    private Ownership CurrentOwnership => !Volume.Owned
        ? Ownership.None
        : Volume.OwnedFormat is OwnedFormat.Digital ? Ownership.Digital : Ownership.Physical;

    protected override void OnParametersSet()
    {
        // The sheet reuses this component when the reader moves to another volume; what was
        // typed or refused for the last one does not carry over.
        if (Volume.VolumeId != _volumeId)
        {
            _volumeId = Volume.VolumeId;
            _draft = null;
            _error = null;
        }
    }

    private async Task OnReadAsync(ReadStatus status)
    {
        await SeriesState.SetReadAsync(Volume, status);
        await Settled.InvokeAsync();
    }

    // No read-date handler raises Settled: editing a date is a correction the reader wants
    // to see land, like switching the edition of an owned volume, so the popover stays open.

    // Only remembers what the input holds. Committing on every change would save half-typed
    // dates and, on a desktop date input, yank focus away mid-edit.
    private void OnReadDateInput(ChangeEventArgs args) => _draft = args.Value?.ToString();

    private async Task OnReadDateKeyDownAsync(KeyboardEventArgs args)
    {
        if (args.Key == "Enter")
        {
            await CommitReadDateAsync();
        }
    }

    // Saves a complete, in-range date that differs from the stored one. An empty draft is
    // never a save: emptying a segment must not erase the stored date, only "Clear date" does.
    private async Task CommitReadDateAsync()
    {
        if (_isCommitting || _draft is null)
        {
            return;
        }

        var date = ReadDateInput.Parse(_draft);
        _draft = null;

        if (date is null)
        {
            // Left incomplete: the input shows nothing, so put the stored date back.
            if (Volume.ReadAt is not null)
            {
                await ShowStoredReadDateAsync();
            }

            return;
        }

        if (date == Volume.ReadAt)
        {
            _error = null;
            return;
        }

        // Checked here, next to the input, rather than left to the state: a refusal printed
        // at the top of the page would sit behind the phone's sheet.
        if (ReadDateInput.ProblemWith(date.Value) is { } problem)
        {
            _error = problem;
            await ShowStoredReadDateAsync();
            return;
        }

        _isCommitting = true;

        try
        {
            if (await SeriesState.SetReadDateAsync(Volume, date))
            {
                _error = null;
            }
            else
            {
                _error = SeriesState.Message;
                await ShowStoredReadDateAsync();
            }
        }
        finally
        {
            _isCommitting = false;
        }
    }

    private async Task OnReadDateClearedAsync()
    {
        _error = null;

        if (!await SeriesState.SetReadDateAsync(Volume, readAt: null))
        {
            _error = SeriesState.Message;
        }
    }

    // The rendered value attribute did not change, so Blazor would leave the DOM showing
    // what the reader picked; write the stored date back by hand, which keeps focus too.
    private async Task ShowStoredReadDateAsync() =>
        await JavaScript.InvokeVoidAsync(
            "lnshelf.setInputValue", _readDateInput, ReadDateInput.Format(Volume.ReadAt));

    private async Task OnOwnershipAsync(Ownership choice)
    {
        if (choice == CurrentOwnership)
        {
            return;
        }

        var wasOwned = Volume.Owned;

        await SeriesState.SetOwnedAsync(
            Volume,
            owned: choice is not Ownership.None,
            choice switch
            {
                Ownership.Physical => OwnedFormat.Physical,
                Ownership.Digital => OwnedFormat.Digital,
                _ => null,
            });

        // Switching the edition of an owned volume is not settled: it is a correction the
        // user may want to see land before deciding they are done.
        if (!wasOwned || choice is Ownership.None)
        {
            await Settled.InvokeAsync();
        }
    }

    private Task OnPickCoverAsync() => CoverPickRequested.InvokeAsync(Volume);

    private async Task OnRemoveCoverAsync()
    {
        await SeriesState.RemoveCoverAsync(Volume);
        await Settled.InvokeAsync();
    }

    private bool IsRemovable =>
        SeriesState.Series is { } series && ManualSeries.IsRemovable(series, Volume);

    /// <summary>
    /// Removes the volume once the reader confirms. The panel closes by itself: the volume it
    /// shows is no longer in the series.
    /// </summary>
    private async Task OnRemoveVolumeAsync()
    {
        var confirmed = await JavaScript.InvokeAsync<bool>(
            "lnshelf.confirm",
            $"Remove Vol {Volume.Number.ToVolumeText()}?\n\nIts read and owned state goes with it.");

        if (!confirmed)
        {
            return;
        }

        await SeriesState.RemoveVolumeAsync(Volume);
        await Settled.InvokeAsync();
    }
}
