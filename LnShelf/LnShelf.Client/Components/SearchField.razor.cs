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

    /// <summary>Anything else the root needs: a class for placement, a data attribute.</summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private string CssClass => ClassNames.Join(AdditionalAttributes, "field");

    private Task OnInputAsync(ChangeEventArgs args) =>
        ValueChanged.InvokeAsync(args.Value?.ToString() ?? string.Empty);
}
