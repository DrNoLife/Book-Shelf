namespace LnShelf.Client.Components;

public partial class SearchField
{
    [Parameter]
    public string Value { get; set; } = string.Empty;

    /// <summary>Raised on every keystroke, not on blur: filtering is meant to feel immediate.</summary>
    [Parameter]
    public EventCallback<string> ValueChanged { get; set; }

    [Parameter]
    public string? Placeholder { get; set; }

    /// <summary>The input's accessible name. The placeholder is not one: it disappears when you type.</summary>
    [Parameter]
    public required string AriaLabel { get; set; }

    /// <summary>For a visible label elsewhere on the page to point at.</summary>
    [Parameter]
    public string? InputId { get; set; }

    /// <summary>
    /// The clear button's accessible name — "Clear the title filter". Null leaves the field
    /// without one; set, a clear mark appears inside the right edge whenever there is text,
    /// and Escape clears as well.
    /// </summary>
    // Opt-in, so the search page keeps its field exactly as it was.
    [Parameter]
    public string? ClearLabel { get; set; }

    /// <summary>Anything else the root needs: a class for placement, a data attribute.</summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private ElementReference _input;

    private string CssClass => ClassNames.Join(AdditionalAttributes, "field");

    private bool IsClearShown => ClearLabel is not null && Value.Length > 0;

    private Task OnInputAsync(ChangeEventArgs args) =>
        ValueChanged.InvokeAsync(args.Value?.ToString() ?? string.Empty);

    private Task OnKeyDownAsync(KeyboardEventArgs args) =>
        args.Key == "Escape" && IsClearShown ? ValueChanged.InvokeAsync(string.Empty) : Task.CompletedTask;

    /// <summary>Focus goes back to the input: the button that had it is about to disappear.</summary>
    private async Task ClearAsync()
    {
        await ValueChanged.InvokeAsync(string.Empty);
        await _input.FocusAsync();
    }
}
