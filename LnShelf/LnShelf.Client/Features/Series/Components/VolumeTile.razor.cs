namespace LnShelf.Client.Features.Series.Components;

public partial class VolumeTile
{
    [Parameter]
    public required VolumeResponse Volume { get; set; }

    [Parameter]
    public bool IsSelectMode { get; set; }

    [Parameter]
    public bool IsSelected { get; set; }

    /// <summary>False while a write is in flight, so a double-click cannot race.</summary>
    [Parameter]
    public bool IsInteractive { get; set; } = true;

    /// <summary>
    /// Bumped whenever a cover override is written. An override reuses one object key,
    /// so without this the browser would keep serving the cover it already cached.
    /// </summary>
    [Parameter]
    public int CoverVersion { get; set; }

    /// <summary>Plain click: open the state menu.</summary>
    [Parameter]
    public EventCallback<VolumeResponse> MenuRequested { get; set; }

    /// <summary>Space: advance the read cycle, which stays the fastest keyboard route.</summary>
    [Parameter]
    public EventCallback<VolumeResponse> ReadCycled { get; set; }

    /// <summary>Shift-click, right-click or shift-space: toggle ownership.</summary>
    [Parameter]
    public EventCallback<VolumeResponse> OwnedToggled { get; set; }

    /// <summary>Click while in select mode.</summary>
    [Parameter]
    public EventCallback<VolumeResponse> SelectionToggled { get; set; }

    /// <summary>
    /// This volume's own title, printed in the caption after the number when it exists.
    /// </summary>
    // The catalogue pass names most volumes; the ones it could not reach stay null, and a
    // null caption prints nothing rather than an invented title.
    [Parameter]
    public string? Title { get; set; }

    /// <summary>Arrow keys, handled by the grid because it knows the layout.</summary>
    [Parameter]
    public EventCallback<(VolumeResponse Volume, string Key)> Navigated { get; set; }

    /// <summary>A cover arrived, by drop or by picker.</summary>
    [Parameter]
    public EventCallback<(VolumeResponse Volume, IBrowserFile File)> CoverSelected { get; set; }

    [Inject]
    public required IJSRuntime JavaScript { get; set; }

    private ElementReference _wrapElement;
    private InputFile? _inputFile;

    private VolumeTileState State => VolumeTileStateMap.For(Volume);

    private string? Label => VolumeTileStateMap.LabelFor(Volume, State);

    /// <summary>
    /// What follows "Vol N" in the caption: the volume's title if it has one, then the
    /// state label if the state carries one. "Vol 9 · Reading", "Vol 15 · 12 Sep".
    /// </summary>
    private IEnumerable<string> CaptionParts
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Title))
            {
                yield return Title;
            }

            if (Label is { } label)
            {
                yield return label;
            }
        }
    }

    /// <summary>
    /// "Read 23 Nov 2021" when the shelf knows when this volume was finished.
    /// </summary>
    // The tile's caption is spoken for — design.md fixes what goes after "Vol N", and a
    // read date is not on that list — so this rides the tooltip. It is never the only
    // channel: the same line is printed in the volume menu, which is a tap away.
    private string? ReadDateText =>
        Volume.ReadAt is { } readAt ? $"Read {readAt.Full}" : null;

    /// <summary>
    /// The override already arrives as the volume's cover key (the projection prefers
    /// it), so the tile only has to defeat the browser cache when there is one.
    /// </summary>
    private string? CoverUrl
    {
        get
        {
            if (Volume.CoverObjectKey is not { } coverObjectKey)
            {
                return null;
            }

            var url = ApiUrl.Cover(coverObjectKey);
            return Volume.HasCoverOverride ? $"{url}?v={CoverVersion}" : url;
        }
    }

    private string StateClass => State switch
    {
        VolumeTileState.Read => "tile--read",
        VolumeTileState.Reading => "tile--reading",
        VolumeTileState.OwnedUnread => "tile--owned",
        VolumeTileState.ToBuy => "tile--tobuy",
        VolumeTileState.Unreleased => "tile--unreleased",
        VolumeTileState.Announced => "tile--announced",
        _ => string.Empty,
    };

    /// <summary>
    /// Spoken state, because the visual channels mean nothing to a screen reader.
    /// </summary>
    private string AccessibleLabel
    {
        get
        {
            var state = State switch
            {
                VolumeTileState.Read => "read",
                VolumeTileState.Reading => "reading",
                VolumeTileState.OwnedUnread => "owned, unread",
                VolumeTileState.ToBuy => "to buy",

                // Spoken with the year the caption has no room for.
                VolumeTileState.Unreleased => Volume.ReleaseDate is { } releaseDate
                    ? $"due {releaseDate.Full}"
                    : "not out yet",
                VolumeTileState.Announced => "announced, no date",
                _ => string.Empty,
            };

            var selection = IsSelectMode ? IsSelected ? ", selected" : ", not selected" : string.Empty;
            return $"Volume {Volume.Number}, {state}{selection}";
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && _inputFile?.Element is { } input)
        {
            await JavaScript.InvokeVoidAsync("lnshelf.registerCoverDrop", _wrapElement, input);
        }
    }

    /// <summary>Opens the tile's own file input — the menu asks the tile to do this.</summary>
    public async Task OpenCoverPickerAsync()
    {
        if (_inputFile?.Element is { } input)
        {
            await JavaScript.InvokeVoidAsync("lnshelf.clickElement", input);
        }
    }

    private Task OnClickAsync(MouseEventArgs args)
    {
        // The guard the disabled attribute used to be; see the markup for why it is not.
        if (!IsInteractive)
        {
            return Task.CompletedTask;
        }

        if (IsSelectMode)
        {
            return SelectionToggled.InvokeAsync(Volume);
        }

        // Shift-click stays the one-gesture route to ownership; a plain click now opens
        // the menu, which is the only place the edition and the cover can be reached.
        return args.ShiftKey
            ? OwnedToggled.InvokeAsync(Volume)
            : MenuRequested.InvokeAsync(Volume);
    }

    private Task OnContextMenuAsync() =>
        IsInteractive ? OwnedToggled.InvokeAsync(Volume) : Task.CompletedTask;

    /// <summary>
    /// Arrows move between tiles even while a write is in flight; space waits for it.
    /// </summary>
    // No Enter case: Enter on a button already fires the native click, which opens the
    // menu through OnClickAsync. Handling it here as well opened the menu and then the
    // click toggled it shut again.
    private Task OnKeyDownAsync(KeyboardEventArgs args)
    {
        switch (args.Key)
        {
            case "ArrowLeft" or "ArrowRight" or "ArrowUp" or "ArrowDown":
                return Navigated.InvokeAsync((Volume, args.Key));

            // Space still cycles read; shift-space toggles owned, mirroring the pointer.
            case " " or "Spacebar" when IsInteractive:
                if (IsSelectMode)
                {
                    return SelectionToggled.InvokeAsync(Volume);
                }

                return args.ShiftKey
                    ? OwnedToggled.InvokeAsync(Volume)
                    : ReadCycled.InvokeAsync(Volume);

            default:
                return Task.CompletedTask;
        }
    }

    private Task OnFileSelectedAsync(InputFileChangeEventArgs args) =>
        CoverSelected.InvokeAsync((Volume, args.File));
}
