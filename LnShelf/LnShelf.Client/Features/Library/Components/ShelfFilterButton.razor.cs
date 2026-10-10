namespace LnShelf.Client.Features.Library.Components;

public partial class ShelfFilterButton
{
    private const string PopoverId = "shelf-filter-panel";

    /// <summary>How many filters are hiding series right now; 0 shows no count.</summary>
    [Parameter]
    public int ActiveCount { get; set; }

    /// <summary>The panel's contents, shared by the sheet and the popover.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Inject]
    public required IJSRuntime JavaScript { get; set; }

    private ElementReference _root;
    private ElementReference _button;
    private ElementReference _popover;

    private bool IsOpen { get; set; }

    /// <summary>Which shape is open: decided when it opens, not on every render.</summary>
    private bool _isSheet;

    /// <summary>Set by an open, consumed by the render that draws the popover.</summary>
    private bool _isFocusPending;

    private string ButtonLabel => ActiveCount switch
    {
        0 => "Filters and sort",
        1 => "Filters and sort, 1 filter on",
        _ => $"Filters and sort, {ActiveCount} filters on",
    };

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // The sheet moves focus in by itself; the popover is told to here, once it exists.
        if (_isFocusPending && IsOpen && !_isSheet)
        {
            _isFocusPending = false;
            await JavaScript.InvokeVoidAsync("lnshelf.focusFirstItem", _popover);
        }
    }

    private async Task ToggleAsync()
    {
        if (IsOpen)
        {
            IsOpen = false;
            return;
        }

        // Asked at the moment of opening, so a tablet rotated between two taps gets whichever
        // shape suits the width it now has.
        _isSheet = await JavaScript.InvokeAsync<bool>("lnshelf.isPhoneLayout");
        _isFocusPending = true;
        IsOpen = true;
    }

    private async Task OnKeyDownAsync(KeyboardEventArgs args)
    {
        if (args.Key == "Escape" && IsOpen)
        {
            await CloseAndReturnFocusAsync();
        }
    }

    /// <summary>Escape and Done hand focus back to the button, which is where the reader left off.</summary>
    private async Task CloseAndReturnFocusAsync()
    {
        IsOpen = false;
        StateHasChanged();
        await _button.FocusAsync();
    }

    /// <summary>
    /// Closes the popover once focus has gone somewhere outside it: a click on the page, or
    /// Tab past its last control. The same deferred check the Dropdown makes.
    /// </summary>
    // Blazor sees focusout before the new focus lands, hence the yield. The sheet is left
    // alone: it is modal, traps focus itself and closes three ways of its own.
    private async Task OnFocusOutAsync()
    {
        if (!IsOpen || _isSheet)
        {
            return;
        }

        await Task.Yield();

        if (await JavaScript.InvokeAsync<bool>("lnshelf.hasFocusWithin", _root))
        {
            return;
        }

        IsOpen = false;
        StateHasChanged();
    }
}
