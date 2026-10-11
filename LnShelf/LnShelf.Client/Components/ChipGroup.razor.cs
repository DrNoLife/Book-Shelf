namespace LnShelf.Client.Components;

public partial class ChipGroup<TValue>
{
    /// <summary>The chips, in order. Each needs a Label; an icon-only chip has nothing to read.</summary>
    [Parameter]
    public required IReadOnlyList<SegmentedOption<TValue>> Options { get; set; }

    /// <summary>The one chosen chip. Ignored in multi-select (<see cref="SelectedValues"/>).</summary>
    [Parameter]
    public TValue? Value { get; set; }

    [Parameter]
    public EventCallback<TValue> ValueChanged { get; set; }

    /// <summary>
    /// Makes the group a multi-select: these chips are on, and a tap reports the chip through
    /// <see cref="ValueToggled"/> rather than <see cref="ValueChanged"/>.
    /// </summary>
    // The caller decides what a toggle means (the status filter's All clears the others), so
    // the group only reports which chip was tapped.
    [Parameter]
    public IReadOnlyCollection<TValue>? SelectedValues { get; set; }

    [Parameter]
    public EventCallback<TValue> ValueToggled { get; set; }

    /// <summary>The group's accessible name — "Status filter", "Your status".</summary>
    [Parameter]
    public required string AriaLabel { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// Wraps onto further lines instead of scrolling sideways, for a set of chips inside a
    /// panel or a sheet, where every choice should be seen at once.
    /// </summary>
    [Parameter]
    public bool Wrap { get; set; }

    /// <summary>A class for placement, merged with the group's own.</summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private string CssClass => ClassNames.Join(AdditionalAttributes, "chips", Wrap ? "chips--wrap" : null);

    private bool IsOn(TValue value) => SelectedValues is { } selected
        ? selected.Contains(value)
        : EqualityComparer<TValue>.Default.Equals(value, Value);

    private Task OnChosenAsync(TValue value) => SelectedValues is not null
        ? ValueToggled.InvokeAsync(value)
        : ValueChanged.InvokeAsync(value);
}
