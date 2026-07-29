using LnShelf.Shared.Shelf;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

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

    /// <summary>Plain click: advance the read cycle.</summary>
    [Parameter]
    public EventCallback<VolumeResponse> ReadCycled { get; set; }

    /// <summary>Shift-click, right-click or long-press: toggle ownership.</summary>
    [Parameter]
    public EventCallback<VolumeResponse> OwnedToggled { get; set; }

    /// <summary>Click while in select mode.</summary>
    [Parameter]
    public EventCallback<VolumeResponse> SelectionToggled { get; set; }

    /// <summary>Arrow keys, handled by the grid because it knows the layout.</summary>
    [Parameter]
    public EventCallback<(VolumeResponse Volume, string Key)> Navigated { get; set; }

    private VolumeTileState State => VolumeTileStateMap.For(Volume);

    private string? Label => VolumeTileStateMap.LabelFor(Volume, State);

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
                VolumeTileState.Unreleased => Label is null ? "not out yet" : $"due {Label}",
                VolumeTileState.Announced => "announced, no date",
                _ => string.Empty,
            };

            var selection = IsSelectMode ? IsSelected ? ", selected" : ", not selected" : string.Empty;
            return $"Volume {Volume.Number}, {state}{selection}";
        }
    }

    private Task OnClickAsync(MouseEventArgs args)
    {
        if (IsSelectMode)
        {
            return SelectionToggled.InvokeAsync(Volume);
        }

        // Shift-click is the pointer route to ownership; the tile also accepts a
        // right-click, and the grid offers the same thing from the keyboard.
        return args.ShiftKey
            ? OwnedToggled.InvokeAsync(Volume)
            : ReadCycled.InvokeAsync(Volume);
    }

    private Task OnContextMenuAsync(MouseEventArgs args) => OwnedToggled.InvokeAsync(Volume);

    private Task OnKeyDownAsync(KeyboardEventArgs args)
    {
        switch (args.Key)
        {
            case "ArrowLeft" or "ArrowRight" or "ArrowUp" or "ArrowDown":
                return Navigated.InvokeAsync((Volume, args.Key));

            // Space cycles read; shift-space toggles owned, mirroring the pointer.
            case " " or "Spacebar" or "Enter":
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
}
