namespace LnShelf.Client.Features.Library;

/// <summary>
/// The volume numbers a library card prints, read off its tick strip.
/// </summary>
// The card's counts are counts, not numbers: with a bonus volume (Torture Princess 1–9 plus
// 7.5) ten volumes are out but the latest is Vol 9, so printing Published as a volume number
// says "Vol 10". The ticks list every volume with its number and whether it is out, so the
// numbers are exact here without a second set of fields on the wire.
public static class CardVolumeNumbers
{
    /// <summary>
    /// The newest volume that is out — "New · Vol N". Null when nothing is out.
    /// </summary>
    // A bonus number is fine here: if 7.5 is the highest out, 7.5 is what just arrived.
    public static decimal? NewestOut(IReadOnlyList<VolumeTickResponse> ticks)
    {
        ArgumentNullException.ThrowIfNull(ticks);

        return ticks.Where(tick => tick.IsOut).Select(tick => (decimal?)tick.Number).Max();
    }

    /// <summary>
    /// The placeholder cover's "Vol N": the newest volume out, else the highest tracked,
    /// else 1 for a series with no volumes at all.
    /// </summary>
    public static decimal ForCover(IReadOnlyList<VolumeTickResponse> ticks)
    {
        ArgumentNullException.ThrowIfNull(ticks);

        return NewestOut(ticks) ?? ticks.Select(tick => (decimal?)tick.Number).Max() ?? 1;
    }

    /// <summary>
    /// The N in "Vol X of N": the highest main volume that is out.
    /// </summary>
    // Main volumes only, so a bonus volume reads "Vol 7.5 of 9" and never "of 7.5". Falls
    // back to the newest out when only bonus volumes are out, and to 0 when nothing is —
    // neither happens where it is shown, since the volume offered is owned and so out.
    public static decimal LastMainOut(IReadOnlyList<VolumeTickResponse> ticks)
    {
        ArgumentNullException.ThrowIfNull(ticks);

        return ticks
            .Where(tick => tick.IsOut && !tick.Number.IsBonusVolume)
            .Select(tick => (decimal?)tick.Number)
            .Max() ?? NewestOut(ticks) ?? 0;
    }
}
