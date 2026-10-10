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

    private async Task OnReadAsync(ReadStatus status)
    {
        await SeriesState.SetReadAsync(Volume, status);
        await Settled.InvokeAsync();
    }

    // Neither read-date handler raises Settled: editing a date is a correction the reader
    // wants to see land, like switching the edition of an owned volume, so the popover
    // stays open. An empty value is the native clear button, and means "no date".
    private Task OnReadDateChangedAsync(ChangeEventArgs args) =>
        SeriesState.SetReadDateAsync(Volume, ReadDateInput.Parse(args.Value?.ToString()));

    private Task OnReadDateClearedAsync() => SeriesState.SetReadDateAsync(Volume, readAt: null);

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
}
