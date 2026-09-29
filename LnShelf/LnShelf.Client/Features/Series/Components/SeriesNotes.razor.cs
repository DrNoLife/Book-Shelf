namespace LnShelf.Client.Features.Series.Components;

public partial class SeriesNotes
{
    [Parameter]
    public required SeriesDetailResponse Series { get; set; }

    /// <summary>
    /// Injected rather than handed a callback, as every other control on this page is
    /// (VolumeGridToolbar): the write is about the series the page already holds.
    /// </summary>
    [Inject]
    public required SeriesState SeriesState { get; set; }

    private bool IsEditing { get; set; }

    /// <summary>What is in the box, which is not the note until it is saved.</summary>
    private string Draft { get; set; } = string.Empty;

    private ElementReference _input;

    private bool _focusWanted;

    private void StartEditing()
    {
        Draft = Series.Notes ?? string.Empty;
        IsEditing = true;

        // Focus is moved after the render that creates the box, not here: the element
        // does not exist yet.
        _focusWanted = true;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_focusWanted)
        {
            return;
        }

        _focusWanted = false;
        await _input.FocusAsync();
    }

    private Task OnKeyDownAsync(KeyboardEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Key is "Escape")
        {
            // Cancel: the draft is dropped, and the note on screen is the saved one again.
            IsEditing = false;
            return Task.CompletedTask;
        }

        // Enter alone is a newline — this is a note, not a search box.
        return args is { Key: "Enter", CtrlKey: true } ? SaveAsync() : Task.CompletedTask;
    }

    /// <summary>Clicking away saves, which is what a text box on a page is expected to do.</summary>
    // Guarded, because Escape has already left edit mode by the time the browser delivers
    // the blur of the box it removed — without this, cancelling would save.
    private Task OnBlurAsync() => IsEditing ? SaveAsync() : Task.CompletedTask;

    /// <summary>
    /// Saves the draft, or nothing at all when it says what the note already says.
    /// </summary>
    // The empty string is not skipped as "nothing to save": it is how the endpoint is told
    // to clear a note (null there means "leave it alone"), so erasing the box and clicking
    // away is the erase.
    //
    // Edit mode is left before the call rather than after it, so the blur of the box being
    // removed cannot start a second save. A refused save reopens it with the draft still
    // in it: closing the box on failure used to throw away whatever had been typed.
    private async Task SaveAsync()
    {
        IsEditing = false;

        var notes = Draft.Trim();

        if (notes == (Series.Notes ?? string.Empty))
        {
            return;
        }

        if (!await SeriesState.SetNotesAsync(notes))
        {
            IsEditing = true;
            _focusWanted = true;
        }
    }
}
