namespace LnShelf.Client.Components;

/// <summary>What a <see cref="Pill"/> says about its words.</summary>
public enum PillTone
{
    /// <summary>A filled neutral: a count that is information ("3 unread").</summary>
    Neutral,

    /// <summary>An outlined neutral: a label that is context ("Coming later").</summary>
    Outline,

    /// <summary>Outlined in the accent: something to act on ("5 to buy").</summary>
    Buy,

    /// <summary>Filled amber: look at this ("New · Vol 16", "4 to review").</summary>
    Attention,

    /// <summary>Green text, no capsule: all well ("Up to date").</summary>
    Ok,
}

public partial class Pill
{
    [Parameter]
    public PillTone Tone { get; set; } = PillTone.Neutral;

    [Parameter]
    public RenderFragment? ChildContent { get; set; }
}
