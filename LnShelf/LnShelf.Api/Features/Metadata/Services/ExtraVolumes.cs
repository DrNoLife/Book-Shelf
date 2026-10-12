namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// Which of a series' volumes are the extras the release source counts beside its main ones:
/// the 2 in "13+2 Volumes", the 1 in "9 Volumes + 1 Extra".
/// </summary>
// An extra is either a bonus volume between the main ones (Torture Princess Vol. 7.5) or a
// whole volume straight past the main count (Solo Leveling's 14 and 15, which IZE Press notes
// as "13+2"). The note never says which, so both kinds count against the one number. Three
// places ask — whether the store is worth asking, how many more the store may add, and whether
// the tracked count agrees with the source's — and they read it from here so they cannot
// disagree.
public static class ExtraVolumes
{
    /// <summary>Whether a whole volume sits in the extras' range just past the main count.</summary>
    public static bool IsWholeExtra(decimal number, int mainCount, int extraCount) =>
        !number.IsBonusVolume && number > mainCount && number <= mainCount + extraCount;

    /// <summary>How many of the series' tracked volumes already fill the extras' places.</summary>
    /// <param name="extraCount">The extras the release source counts.</param>
    public static int CountTracked(Series series, int extraCount)
    {
        ArgumentNullException.ThrowIfNull(series);

        return series.Volumes.Count(volume =>
            volume.Number.IsBonusVolume
            || (series.ReleaseSourceVolumeCount is { } mainCount
                && IsWholeExtra(volume.Number, mainCount, extraCount)));
    }

    /// <summary>
    /// How many tracked whole volumes past the main count the source's own extras account for:
    /// Solo Leveling's 14 and 15 under "13+2", so a shelf of fifteen agrees with a count of 13.
    /// </summary>
    /// <param name="mainCount">The release source's main count — the 13 in "13+2".</param>
    /// <param name="extraCount">The extras it counts beside them — the 2.</param>
    // Bonus volumes take their places first, as they do when WholeExtraVolumeWriter decides how
    // many it may add: "9 Volumes + 1 Extra" with Vol. 7.5 tracked has no place left, so a
    // whole Vol. 10 there is still a volume the source does not know of.
    public static int CountWholeExtrasWithin(Series series, int mainCount, int extraCount)
    {
        ArgumentNullException.ThrowIfNull(series);

        var placesLeft = extraCount - series.Volumes.Count(volume => volume.Number.IsBonusVolume);
        var wholeExtras = series.Volumes.Count(volume => IsWholeExtra(volume.Number, mainCount, extraCount));

        return Math.Clamp(wholeExtras, 0, Math.Max(placesLeft, 0));
    }
}
