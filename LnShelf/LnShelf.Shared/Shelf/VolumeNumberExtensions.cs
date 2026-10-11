using System.Globalization;

namespace LnShelf.Shared.Shelf;

/// <summary>
/// A volume number as people write it: "7", and "7.5" for a bonus volume.
/// </summary>
// Volume numbers are decimals because bonus volumes exist between the main ones
// (Torture Princess Vol. 7.5, Classroom of the Elite Vol. 11.5). Two traps come with that,
// and this is the one place that steps around both: a decimal read back with a scale prints
// "7.0", and plain interpolation in a Danish browser prints "7,5". Every number shown to a
// reader or sent to a source goes through here.
public static class VolumeNumberExtensions
{
    extension(decimal volumeNumber)
    {
        /// <summary>"7", "7.5" — invariant, with no trailing zeros.</summary>
        public string ToVolumeText() => volumeNumber.ToString("0.##", CultureInfo.InvariantCulture);

        /// <summary>
        /// Whether this is a bonus volume between two main ones rather than a main volume.
        /// </summary>
        public bool IsBonusVolume => volumeNumber != decimal.Truncate(volumeNumber);
    }
}
