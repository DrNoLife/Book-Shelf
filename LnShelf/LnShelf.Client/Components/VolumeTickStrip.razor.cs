using LnShelf.Shared.Shelf;
using Microsoft.AspNetCore.Components;

namespace LnShelf.Client.Components;

public partial class VolumeTickStrip
{
    /// <summary>Every volume the shelf knows of, in number order.</summary>
    [Parameter]
    public required IReadOnlyList<VolumeTickResponse> Ticks { get; set; }

    /// <summary>
    /// How many ticks are drawn before the rest become a "+N" tail.
    /// </summary>
    // 24 is where a strip still fits a table cell at 1440px without the ticks losing
    // their gaps; past it the strip stops being scannable and starts being a smear.
    [Parameter]
    public int Cap { get; set; } = 24;

    private VolumeTickStripLayout Layout => VolumeTickStripLayout.For(Ticks, Cap);

    private static string TickClass(VolumeTickKind kind) => kind switch
    {
        VolumeTickKind.Read => "tick--read",
        VolumeTickKind.OwnedUnread => "tick--owned",
        VolumeTickKind.ToBuy => "tick--tobuy",
        VolumeTickKind.NotOut => "tick--notout",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown tick kind."),
    };

    private static string TickTitle(VolumeTickResponse tick) => $"Vol {tick.Number} · {KindWords(tick.Kind)}";

    /// <summary>The fixed terminology, never a synonym (design.md).</summary>
    private static string KindWords(VolumeTickKind kind) => kind switch
    {
        VolumeTickKind.Read => "read",
        VolumeTickKind.OwnedUnread => "owned, unread",
        VolumeTickKind.ToBuy => "to buy",
        VolumeTickKind.NotOut => "not out yet",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown tick kind."),
    };

    /// <summary>
    /// The whole strip in words, because the ticks mean nothing to a screen reader —
    /// and it counts every volume, including the ones folded into the tail.
    /// </summary>
    private string AccessibleLabel
    {
        get
        {
            var parts = new List<string>();

            foreach (var kind in (VolumeTickKind[])[
                VolumeTickKind.Read, VolumeTickKind.OwnedUnread, VolumeTickKind.ToBuy, VolumeTickKind.NotOut])
            {
                var count = Ticks.Count(tick => tick.Kind == kind);
                if (count > 0)
                {
                    parts.Add($"{count} {KindWords(kind)}");
                }
            }

            return parts.Count == 0 ? "No volumes tracked" : string.Join(", ", parts);
        }
    }
}
