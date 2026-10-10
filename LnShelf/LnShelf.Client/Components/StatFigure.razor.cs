namespace LnShelf.Client.Components;

/// <summary>How large a <see cref="StatFigure"/>'s number is. The sizes are the design's own; there is no in-between.</summary>
public enum StatSize
{
    /// <summary>30px: a figure in a narrow card.</summary>
    Small,

    /// <summary>40px: a figure in a preview.</summary>
    Medium,

    /// <summary>44px: a figure inside a panel.</summary>
    Large,

    /// <summary>64px: the library header's read / owned / out.</summary>
    Hero,
}

public partial class StatFigure
{
    /// <summary>The number, already formatted. A string so "—" and "12 / 14" are possible.</summary>
    [Parameter]
    public required string Value { get; set; }

    /// <summary>The word for it: "read", "owned", "volumes read".</summary>
    [Parameter]
    public required string Label { get; set; }

    [Parameter]
    public StatSize Size { get; set; } = StatSize.Large;

    /// <summary>The label under the number instead of beside it, for a grid of totals.</summary>
    [Parameter]
    public bool Stacked { get; set; }

    /// <summary>The number in --faint: the figure that is context, not the news ("out", beside "read" and "owned").</summary>
    [Parameter]
    public bool Quiet { get; set; }

    /// <summary>A trailing note after the label: "+ 1 announced".</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    private string SizeClass => Size switch
    {
        StatSize.Small => "stat--small",
        StatSize.Medium => "stat--medium",
        StatSize.Large => "stat--large",
        StatSize.Hero => "stat--hero",
        _ => throw new ArgumentOutOfRangeException(nameof(Size), Size, "Unknown stat size."),
    };
}
