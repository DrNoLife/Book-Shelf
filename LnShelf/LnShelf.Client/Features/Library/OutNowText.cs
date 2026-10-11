namespace LnShelf.Client.Features.Library;

/// <summary>
/// The line under a series in the phone's "Out now": which volumes are out and not owned.
/// </summary>
// A range only when it is one. The volumes to buy need not be contiguous — a gap of owned
// volumes in the middle is common after an import — and "Vol 4–9 are out" would then claim
// volumes the reader already has. That is the reason the desktop list prints a count instead.
public static class OutNowText
{
    public static string For(OutNowEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var toBuy = entry.Card.VolumeTicks
            .Where(tick => tick.Kind == VolumeTickKind.ToBuy)
            .Select(tick => tick.Number)
            .Order()
            .ToList();

        if (toBuy.Count <= 1)
        {
            return $"Vol {entry.FirstVolumeNumber.ToVolumeText()} is out";
        }

        // A bonus volume never sits inside a range, as in VolumeNumberText: "Vol 7–8" would hide it.
        var isRun = !toBuy.Any(number => number.IsBonusVolume) && toBuy[^1] - toBuy[0] + 1 == toBuy.Count;
        return isRun
            ? $"Vol {toBuy[0].ToVolumeText()}–{toBuy[^1].ToVolumeText()} are out"
            : $"{toBuy.Count} out, from Vol {toBuy[0].ToVolumeText()}";
    }
}
