namespace LnShelf.Client.Features.Series.Components;

public partial class VolumePopover
{
    /// <summary>
    /// Nominal size, used to decide the flip before the panel is measurable: the head, the
    /// reading and ownership choices, "Set cover…", the footer and the gaps between.
    /// </summary>
    public const double Width = 296;
    public const double Height = 330;

    /// <summary>
    /// The nominal size with a blurb in it: wider, because prose at 296px is a short line,
    /// and taller by the clamped blurb.
    /// </summary>
    // The placement is computed before the panel exists, from these numbers rather than
    // from a measurement — so a panel that grew and did not say so would be placed as if
    // it were still its smallest and hang off the bottom of the screen. The rows that only
    // some volumes have are named and counted rather than left inside one number that
    // drifts.
    public const double DescribedWidth = 320;
    public const double DescribedHeight = 430;

    /// <summary>"Buy Vol 18 on Saxo", which only a released, unowned volume shows.</summary>
    private const double BuyRowHeight = 60;

    /// <summary>"Remove cover", which only a volume carrying an override shows.</summary>
    private const double RemoveCoverRowHeight = 44;

    /// <summary>"Read on [date] Clear date", which only a read volume shows.</summary>
    private const double ReadDateRowHeight = 52;

    /// <summary>"Using Apple Books (your choice)", which only a volume whose date was picked shows.</summary>
    private const double ChoiceNoteHeight = 21;

    /// <summary>The nominal size this volume's panel will take, blurb or no blurb.</summary>
    public static double WidthFor(VolumeResponse volume) =>
        HasBlurb(volume) ? DescribedWidth : Width;

    /// <param name="hasChoiceNote">Whether the reader picked this volume's date source.</param>
    public static double HeightFor(VolumeResponse volume, bool hasChoiceNote = false)
    {
        ArgumentNullException.ThrowIfNull(volume);

        var height = HasBlurb(volume) ? DescribedHeight : Height;

        if (hasChoiceNote)
        {
            height += ChoiceNoteHeight;
        }

        if (volume.ReadStatus is ReadStatus.Read)
        {
            height += ReadDateRowHeight;
        }

        if (IsBuyable(volume))
        {
            height += BuyRowHeight;
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

    private static bool IsBuyable(VolumeResponse volume) => VolumeDetails.IsBuyable(volume);

    [Parameter]
    public required VolumeResponse Volume { get; set; }

    [Parameter]
    public required PopoverPlacement Placement { get; set; }

    /// <summary>The source the reader picked for this volume's release date, or null for automatic.</summary>
    [Parameter]
    public MetadataSourceName? DateChoice { get; set; }

    /// <summary>The series' title, which the Saxo search is built from.</summary>
    [Parameter]
    public required string SeriesTitle { get; set; }

    [Parameter]
    public bool IsBusy { get; set; }

    [Parameter]
    public EventCallback Closed { get; set; }

    /// <summary>Asks the tile to open its file picker — the tile owns the input.</summary>
    [Parameter]
    public EventCallback<VolumeResponse> CoverPickRequested { get; set; }

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

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // Focus moves into the panel so the keyboard can reach it and so focus-out
            // becomes a usable click-away signal.
            await JavaScript.InvokeVoidAsync("lnshelf.focusFirstItem", _menuElement);
        }
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

            default:
                return;
        }
    }

    /// <summary>
    /// Click-away and focus-away close the panel — which is also what Tab past its last
    /// control does. The yield lets the clicked control's own handler run first, which
    /// would otherwise be cancelled by the re-render.
    /// </summary>
    private async Task OnFocusOutAsync()
    {
        await Task.Yield();

        if (await JavaScript.InvokeAsync<bool>("lnshelf.hasFocusWithin", _menuElement))
        {
            return;
        }

        await Closed.InvokeAsync();
    }
}
