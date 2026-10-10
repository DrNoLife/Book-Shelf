namespace LnShelf.Client.Components;

/// <summary>
/// One segment of a <see cref="SegmentedControl{TValue}"/>: a word, an icon, or both.
/// </summary>
// A null Label means an icon-only segment, which is what decides whether the segment
// gets the wider word treatment — the density toggle is icons, the two view toggles
// are words. Detail is a quiet figure after the label — the search page's "LN 8" — drawn
// fainter than the word so the choice still reads first.
public sealed record SegmentedOption<TValue>(
    TValue Value,
    string? Label,
    string Title,
    IconName? Icon = null,
    string? Detail = null);

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

    /// <summary>
    /// Fills the width it is given, every segment equally wide. For the three-way choices
    /// inside a panel (Unread / Reading / Read), where ragged segments would look unfinished.
    /// </summary>
    [Parameter]
    public bool Stretch { get; set; }

    /// <summary>
    /// Sits on the page colour instead of the surface colour, for a control placed on a
    /// --surf panel — otherwise the container would vanish into the panel behind it.
    /// </summary>
    [Parameter]
    public bool Inset { get; set; }

    /// <summary>Why the control is inert, appended to each segment's own title.</summary>
    [Parameter]
    public string? DisabledTitle { get; set; }

    private string TitleFor(SegmentedOption<TValue> option) =>
        Disabled && DisabledTitle is { } reason ? $"{option.Title} — {reason}" : option.Title;

    private Task OnChosenAsync(TValue value) => ValueChanged.InvokeAsync(value);
}
