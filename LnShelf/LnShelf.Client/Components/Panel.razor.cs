namespace LnShelf.Client.Components;

/// <summary>How a <see cref="Panel"/> looks.</summary>
public enum PanelTone
{
    Default,

    /// <summary>"Sources disagree": the amber line and wash. For the one thing on a page that needs a look.</summary>
    Warning,
}

public partial class Panel
{
    /// <summary>Default: the plain card. Warning: the amber conflict treatment.</summary>
    [Parameter]
    public PanelTone Tone { get; set; } = PanelTone.Default;

    /// <summary>Drops the inner padding, for a panel whose content brings its own (a list, a table).</summary>
    [Parameter]
    public bool Flush { get; set; }

    [Parameter]
    public string? AriaLabel { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>Anything else the element needs: id, role, aria-labelledby, and a class for placement.</summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private string CssClass => ClassNames.Join(
        AdditionalAttributes,
        "panel",
        Tone is PanelTone.Warning ? "panel--warning" : null,
        Flush ? "panel--flush" : null);
}
