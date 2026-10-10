namespace LnShelf.Client.Components;

/// <summary>What a <see cref="SignalDot"/> signals. Two colours, each with one meaning.</summary>
public enum SignalTone
{
    /// <summary>--ok: healthy, done.</summary>
    Ok,

    /// <summary>--amber: something wants a look.</summary>
    Attention,

    /// <summary>--faint: nothing to say yet ("not synced").</summary>
    Quiet,
}

public partial class SignalDot
{
    [Parameter]
    public SignalTone Tone { get; set; } = SignalTone.Ok;

    /// <summary>10px instead of 8px, for a panel's lead mark.</summary>
    [Parameter]
    public bool Large { get; set; }
}
