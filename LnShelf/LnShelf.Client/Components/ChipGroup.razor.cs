namespace LnShelf.Client.Components;

public partial class ChipGroup<TValue>
{
    /// <summary>The chips, in order. Each needs a Label; an icon-only chip has nothing to read.</summary>
    [Parameter]
    public required IReadOnlyList<SegmentedOption<TValue>> Options { get; set; }

    [Parameter]
    public required TValue Value { get; set; }

    [Parameter]
    public EventCallback<TValue> ValueChanged { get; set; }

    /// <summary>The group's accessible name — "Status filter", "Your status".</summary>
    [Parameter]
    public required string AriaLabel { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>A class for placement, merged with the group's own.</summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private string CssClass => ClassNames.Join(AdditionalAttributes, "chips");

    private Task OnChosenAsync(TValue value) => ValueChanged.InvokeAsync(value);
}
