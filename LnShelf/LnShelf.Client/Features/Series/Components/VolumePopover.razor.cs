using LnShelf.Shared.Shelf;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace LnShelf.Client.Features.Series.Components;

public partial class VolumePopover
{
    /// <summary>Nominal size, used to decide the flip before the menu is measurable.</summary>
    public const double Width = 208;
    public const double Height = 250;

    [Parameter]
    public required VolumeResponse Volume { get; set; }

    [Parameter]
    public required PopoverPlacement Placement { get; set; }

    [Parameter]
    public bool IsBusy { get; set; }

    [Parameter]
    public EventCallback Closed { get; set; }

    /// <summary>Asks the tile to open its file picker — the tile owns the input.</summary>
    [Parameter]
    public EventCallback<VolumeResponse> CoverPickRequested { get; set; }

    [Inject]
    public required SeriesState SeriesState { get; set; }

    [Inject]
    public required IJSRuntime JavaScript { get; set; }

    private ElementReference _menuElement;

    private sealed record ReadChoice(ReadStatus Status, string Label, string SwatchClass);

    private static readonly IReadOnlyList<ReadChoice> ReadChoices =
    [
        new(ReadStatus.Unread, "Unread", "swatch--unread"),
        new(ReadStatus.Reading, "Reading", "swatch--reading"),
        new(ReadStatus.Read, "Read", "swatch--read"),
    ];

    private static readonly IReadOnlyList<OwnedFormat> Editions =
        [OwnedFormat.Physical, OwnedFormat.Digital];

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // Focus moves into the menu so the keyboard can reach it and so focus-out
            // becomes a usable click-away signal.
            await JavaScript.InvokeVoidAsync("lnshelf.focusFirstItem", _menuElement);
        }
    }

    private async Task OnReadAsync(ReadStatus status)
    {
        await SeriesState.SetReadAsync(Volume, status);
        await Closed.InvokeAsync();
    }

    private async Task OnOwnedToggleAsync()
    {
        await SeriesState.SetOwnedAsync(Volume, !Volume.Owned);
        await Closed.InvokeAsync();
    }

    // The edition stays open: switching physical to digital is a correction the user
    // may want to see land on the tile before deciding they are done.
    private Task OnEditionAsync(OwnedFormat edition) =>
        SeriesState.SetOwnedAsync(Volume, owned: true, edition);

    private Task OnPickCoverAsync() => CoverPickRequested.InvokeAsync(Volume);

    private async Task OnRemoveCoverAsync()
    {
        await SeriesState.RemoveCoverAsync(Volume);
        await Closed.InvokeAsync();
    }

    private async Task OnKeyDownAsync(KeyboardEventArgs args)
    {
        switch (args.Key)
        {
            case "Escape":
                await Closed.InvokeAsync();
                return;

            case "ArrowDown" or "ArrowUp":
                await JavaScript.InvokeVoidAsync(
                    "lnshelf.moveMenuFocus", _menuElement, args.Key == "ArrowDown" ? 1 : -1);
                return;

            // Tab out of a menu means "done with the menu", the same as clicking away.
            case "Tab":
                await Closed.InvokeAsync();
                return;

            default:
                return;
        }
    }

    /// <summary>
    /// Click-away and focus-away close the menu. The yield lets the clicked item's own
    /// handler run first, which would otherwise be cancelled by the re-render.
    /// </summary>
    private async Task OnFocusOutAsync(FocusEventArgs args)
    {
        await Task.Yield();

        if (await JavaScript.InvokeAsync<bool>("lnshelf.hasFocusWithin", _menuElement))
        {
            return;
        }

        await Closed.InvokeAsync();
    }
}
