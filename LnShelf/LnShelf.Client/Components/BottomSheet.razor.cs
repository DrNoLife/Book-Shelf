namespace LnShelf.Client.Components;

public partial class BottomSheet : IAsyncDisposable
{
    /// <summary>The dialog's accessible name — "Volume 18", "Series actions".</summary>
    [Parameter]
    public required string AriaLabel { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>Raised by Done, Escape and the scrim alike. The owner removes the sheet.</summary>
    [Parameter]
    public EventCallback Closed { get; set; }

    [Inject]
    public required IJSRuntime JavaScript { get; set; }

    private ElementReference _sheetElement;
    private ElementReference _scrimElement;
    private DotNetObjectReference<BottomSheet>? _selfReference;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // Focus moves into the sheet the moment it exists, so a keyboard or a screen reader
        // lands in the dialog rather than behind it.
        if (firstRender)
        {
            await JavaScript.InvokeVoidAsync("lnshelf.focusFirstItem", _sheetElement);

            _selfReference = DotNetObjectReference.Create(this);
            await JavaScript.InvokeVoidAsync("lnshelf.registerSheetDrag", _sheetElement, _scrimElement, _selfReference);
        }
    }

    /// <summary>Called by the drag gesture once released far or fast enough: the same close as Done.</summary>
    [JSInvokable]
    public Task CloseFromDragAsync() => CloseAsync();

    public async ValueTask DisposeAsync()
    {
        try
        {
            await JavaScript.InvokeVoidAsync("lnshelf.unregisterSheetDrag", _sheetElement);
        }
        catch (JSDisconnectedException)
        {
            // Nothing to clean up once the page is gone.
        }

        _selfReference?.Dispose();
    }

    private Task CloseAsync() => Closed.InvokeAsync();

    private Task OnKeyDownAsync(KeyboardEventArgs args) =>
        args.Key == "Escape" ? CloseAsync() : Task.CompletedTask;

    // The two guards are the focus trap: tabbing off either end of the sheet lands on a guard,
    // which hands focus straight to the control at the other end.
    private async Task FocusFirstAsync() =>
        await JavaScript.InvokeVoidAsync("lnshelf.focusFirstItem", _sheetElement);

    private async Task FocusLastAsync() =>
        await JavaScript.InvokeVoidAsync("lnshelf.focusLastItem", _sheetElement);
}
