using LnShelf.Shared.Shelf;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace LnShelf.Client.Features.Series.Components;

public partial class VolumePopover
{
    /// <summary>
    /// Nominal size, used to decide the flip before the menu is measurable: the head, the
    /// three read rows, the owned row, "Set cover…", the footer and the hairlines between.
    /// </summary>
    public const double Width = 208;
    public const double Height = 250;

    /// <summary>
    /// The nominal size with a blurb in it: wider, because 208px of prose is four words a
    /// line, and taller by the clamped blurb plus its hairline.
    /// </summary>
    // The placement is computed before the menu exists, from these numbers rather than
    // from a measurement — so a menu that grew and did not say so would be placed as if it
    // were still 250 tall and hang off the bottom of the screen. Which is exactly what
    // happened: measured at 400x850 the described menu of an owned volume renders 403px
    // tall against the 372 this claimed, and hung 18px off the bottom of the screen with
    // "Set cover…" and the drop hint below the fold. The rows that only some volumes have
    // are now named and counted rather than left inside one number that drifts.
    public const double DescribedWidth = 248;
    public const double DescribedHeight = 386;

    /// <summary>The edition buttons, which only an owned volume shows.</summary>
    private const double EditionRowHeight = 33;

    /// <summary>"Remove cover", which only a volume carrying an override shows.</summary>
    private const double RemoveCoverRowHeight = 32;

    /// <summary>The nominal size this volume's menu will take, blurb or no blurb.</summary>
    public static double WidthFor(VolumeResponse volume) =>
        HasBlurb(volume) ? DescribedWidth : Width;

    public static double HeightFor(VolumeResponse volume)
    {
        var height = HasBlurb(volume) ? DescribedHeight : Height;

        if (volume.Owned)
        {
            height += EditionRowHeight;
        }

        if (volume.HasCoverOverride)
        {
            height += RemoveCoverRowHeight;
        }

        return height;
    }

    private static bool HasBlurb(VolumeResponse volume)
    {
        ArgumentNullException.ThrowIfNull(volume);
        return !string.IsNullOrWhiteSpace(volume.Description);
    }

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

    private bool HasDescription => HasBlurb(Volume);

    /// <summary>
    /// The blurb split into paragraphs, the shape the API's sanitizer guarantees (a blank
    /// line between paragraphs) and the same split the hero's synopsis does.
    /// </summary>
    private IReadOnlyList<string> DescriptionParagraphs =>
        Volume.Description is { } description
            ? description.Split(
                "\n\n",
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [];

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
