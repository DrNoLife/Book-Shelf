namespace LnShelf.Client.Features.Series.Components;

public partial class SeriesProgress
{
    [Parameter]
    public required SeriesDetailResponse Series { get; set; }

    /// <summary>
    /// One kind per volume for the phone's segment strip: the six tile states folded into the
    /// strip's four, the way the library's ticks fold them (VolumeTickResponse.Kind).
    /// </summary>
    private IReadOnlyList<VolumeTickKind> VolumeKinds =>
    [
        .. Series.Volumes.Select(volume => VolumeTileStateMap.For(volume) switch
        {
            VolumeTileState.Read => VolumeTickKind.Read,
            VolumeTileState.OwnedUnread => VolumeTickKind.OwnedUnread,
            VolumeTileState.Reading => volume.Owned ? VolumeTickKind.OwnedUnread : VolumeTickKind.ToBuy,
            VolumeTileState.ToBuy => VolumeTickKind.ToBuy,
            _ => VolumeTickKind.NotOut,
        }),
    ];

    /// <summary>Volumes the shelf tracks that are not out yet.</summary>
    private int AnnouncedCount => Series.Counts.NotOut;

    /// <summary>
    /// "3 sitting unread. 3 to buy." — in the fixed terminology, and honest when both
    /// numbers are zero.
    /// </summary>
    private string ProseLine
    {
        get
        {
            var counts = Series.Counts;

            if (counts.Backlog == 0 && counts.ToBuy == 0)
            {
                return counts.NotOut > 0
                    ? $"Caught up. {counts.NotOut} more to come."
                    : "Caught up, and that is the whole series.";
            }

            var parts = new List<string>();
            if (counts.Backlog > 0) parts.Add($"{counts.Backlog} sitting unread.");
            if (counts.ToBuy > 0) parts.Add($"{counts.ToBuy} to buy.");

            return string.Join(" ", parts);
        }
    }

    /// <summary>
    /// "Started 12 Mar 2023 · last read 6 Jul 2026 · 12 volumes priced · 480 kr · avg 40 kr".
    /// </summary>
    // Spend only when prices exist. An imported library has none, and "0 kr · avg 0 kr"
    // would be a claim about money that was never recorded.
    private string? FootLine
    {
        get
        {
            var parts = new List<string>();
            if (Series.StartedAt is { } started) parts.Add($"Started {started.Full}");
            if (Series.LastReadAt is { } lastRead) parts.Add($"last read {lastRead.Full}");

            if (Series.Spend is { } spend)
            {
                parts.Add($"{spend.PricedCount} volumes priced");
                parts.Add($"{spend.Total.ToString("0", CultureInfo.InvariantCulture)} kr");
                parts.Add($"avg {spend.Average.ToString("0", CultureInfo.InvariantCulture)} kr");
            }

            return parts.Count == 0 ? null : string.Join(" · ", parts);
        }
    }
}
