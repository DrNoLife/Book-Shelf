namespace LnShelf.Client.Components;

/// <summary>
/// One segment of a <see cref="SegmentedControl{TValue}"/>: a word, an icon, or both.
/// </summary>
// A null Label means an icon-only segment, which is what decides whether the segment
// gets the wider word treatment — the density toggle is icons, the two view toggles
// are words.
public sealed record SegmentedOption<TValue>(TValue Value, string? Label, string Title, IconName? Icon = null);

public partial class SegmentedControl<TValue>
{
    [Parameter]
    public required IReadOnlyList<SegmentedOption<TValue>> Options { get; set; }

    [Parameter]
    public required TValue Value { get; set; }

    [Parameter]
    public EventCallback<TValue> ValueChanged { get; set; }

    /// <summary>The control's accessible name — "Library view", "Volume view".</summary>
    [Parameter]
    public required string AriaLabel { get; set; }

    /// <summary>
    /// Inert, and visibly so. Disabled rather than hidden: a control that vanishes reads
    /// as a bug, one that says why it is inert does not.
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>Why the control is inert, appended to each segment's own title.</summary>
    [Parameter]
    public string? DisabledTitle { get; set; }

    private string TitleFor(SegmentedOption<TValue> option) =>
        Disabled && DisabledTitle is { } reason ? $"{option.Title} — {reason}" : option.Title;

    private Task OnChosenAsync(TValue value) => ValueChanged.InvokeAsync(value);
}
