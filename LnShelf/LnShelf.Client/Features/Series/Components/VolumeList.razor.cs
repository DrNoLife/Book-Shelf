using LnShelf.Shared.Shelf;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace LnShelf.Client.Features.Series.Components;

public partial class VolumeList
{
    [Parameter]
    public required IReadOnlyList<VolumeResponse> Volumes { get; set; }

    /// <summary>
    /// Whether to print each volume's own title beside its number.
    /// </summary>
    // A row has the width a tile's caption does not, so this is where a title is least
    // cramped — but it is still the same preference, and the same volumes have nothing
    // to print.
    [Parameter]
    public bool ShowTitles { get; set; }

    /// <summary>False while a write is in flight, so a double-click cannot race.</summary>
    [Parameter]
    public bool IsInteractive { get; set; } = true;

    /// <summary>Click or Enter: open the state menu, exactly as a tile does.</summary>
    [Parameter]
    public EventCallback<VolumeResponse> MenuRequested { get; set; }

    /// <summary>Space: advance the read cycle — the same keyboard shortcut as the grid.</summary>
    [Parameter]
    public EventCallback<VolumeResponse> ReadCycled { get; set; }

    /// <summary>Shift-click or shift-space: toggle ownership.</summary>
    [Parameter]
    public EventCallback<VolumeResponse> OwnedToggled { get; set; }

    /// <summary>Arrow keys, handled by the section because it owns the measurement.</summary>
    [Parameter]
    public EventCallback<(VolumeResponse Volume, string Key)> Navigated { get; set; }

    private static string StateClass(VolumeTileState state) => state switch
    {
        VolumeTileState.Read => "row--read",
        VolumeTileState.Reading => "row--reading",
        VolumeTileState.OwnedUnread => "row--owned",
        VolumeTileState.ToBuy => "row--tobuy",
        VolumeTileState.Unreleased => "row--unreleased",
        VolumeTileState.Announced => "row--announced",
        _ => string.Empty,
    };

    private static string AccessibleLabel(VolumeResponse volume, VolumeTileState state)
    {
        var edition = volume.Owned ? $", {volume.OwnedFormat?.ToString() ?? "owned"}" : string.Empty;
        return $"Volume {volume.Number}, {VolumeTileStateMap.NameFor(state).ToLowerInvariant()}{edition}";
    }

    /// <summary>
    /// The tile's gesture, unchanged: a plain click opens the menu, shift-click is the
    /// one-gesture route to ownership.
    /// </summary>
    private Task OnClickAsync(MouseEventArgs args, VolumeResponse volume) =>
        args.ShiftKey
            ? OwnedToggled.InvokeAsync(volume)
            : MenuRequested.InvokeAsync(volume);

    private Task OnKeyDownAsync(KeyboardEventArgs args, VolumeResponse volume)
    {
        switch (args.Key)
        {
            case "ArrowLeft" or "ArrowRight" or "ArrowUp" or "ArrowDown":
                return Navigated.InvokeAsync((volume, args.Key));

            case " " or "Spacebar":
                return args.ShiftKey
                    ? OwnedToggled.InvokeAsync(volume)
                    : ReadCycled.InvokeAsync(volume);

            default:
                return Task.CompletedTask;
        }
    }
}
