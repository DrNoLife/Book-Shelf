namespace LnShelf.Client.Components;

/// <summary>
/// What a tick strip actually draws: the ticks that fit, and how many were folded
/// into the "+N" tail.
/// </summary>
// Split out of the component so the cap has one home and can be tested without a
// browser — a 42-volume manga in a table cell is the case that decided the cap.
public sealed record VolumeTickStripLayout
{
    /// <summary>The ticks drawn, in volume-number order.</summary>
    public required IReadOnlyList<VolumeTickResponse> Visible { get; init; }

    /// <summary>How many volumes the tail stands for. Zero means no tail.</summary>
    public required int HiddenCount { get; init; }

    /// <summary>What the tail's tooltip says. Null when there is no tail.</summary>
    public string? HiddenLabel { get; init; }

    /// <summary>
    /// The first <paramref name="cap"/> ticks, with the rest counted into the tail.
    /// </summary>
    // The first N rather than the last N: the strip reads left to right as volume 1
    // onwards, and a strip that silently started at volume 19 would misrepresent
    // which end of the series it was showing.
    public static VolumeTickStripLayout For(IReadOnlyList<VolumeTickResponse> ticks, int cap)
    {
        ArgumentNullException.ThrowIfNull(ticks);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cap);

        if (ticks.Count <= cap)
        {
            return new VolumeTickStripLayout { Visible = ticks, HiddenCount = 0 };
        }

        var visible = ticks.Take(cap).ToList();
        var hidden = ticks.Skip(cap).ToList();

        return new VolumeTickStripLayout
        {
            Visible = visible,
            HiddenCount = hidden.Count,
            HiddenLabel = $"{hidden.Count} more — volumes {hidden[0].Number.ToVolumeText()}–{hidden[^1].Number.ToVolumeText()}",
        };
    }
}
