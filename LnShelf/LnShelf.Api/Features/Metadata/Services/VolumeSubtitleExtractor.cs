using System.Text.RegularExpressions;

namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// Reduces a record's own full title to the part that belongs to the volume: its subtitle,
/// or nothing at all.
/// </summary>
// A source names a volume the way a shop does — "86—EIGHTY-SIX, Vol. 3: Run Through the
// Battlefront (Finish)", "That Time I Got Reincarnated as a Slime, Vol. 17" — and the shelf
// already knows the series and the number from the row the title is being written to. What
// is left over is the only new fact in the string, and in a hundred-pixel tile caption it is
// also the only part that fits: printed whole, the ellipsis eats the distinctive half and
// every tile reads the same.
//
// Deliberately source-agnostic and applied at the write boundary (VolumeFactWriter) rather
// than in either mapper. Both sources title volumes this way, the rule is the same for both,
// and the matcher upstream still needs the untouched string — the "(light novel)" marker it
// reads for the medium is exactly what this strips.
public static partial class VolumeSubtitleExtractor
{
    /// <param name="recordTitle">The source's own title for the record, untouched.</param>
    /// <returns>
    /// The subtitle to store, which is empty when the record named the volume and it simply
    /// has no subtitle. Empty is an answer rather than a gap — see <see cref="Volume.Title"/>.
    /// </returns>
    public static string Extract(string? recordTitle, int volumeNumber)
    {
        if (string.IsNullOrWhiteSpace(recordTitle))
        {
            return string.Empty;
        }

        // No marker at all is the honest "this title is already only the volume's own part":
        // Open Library carries editions titled plainly "Run Through the Battlefield", and
        // there is nothing in one to cut away.
        var subtitle = AfterVolumeMarker(recordTitle, volumeNumber) ?? recordTitle;

        return Tidy(subtitle);
    }

    /// <summary>
    /// Whatever follows this volume's own number, once the number has been found.
    /// </summary>
    // The first marker naming *this* number wins, not the last. A subtitle can contain
    // digits of its own ("Vol. 5: 1943"), so scanning backwards would cut in the wrong
    // place; the volume's number is the leftmost thing in these titles that could be it.
    //
    // The series title is deliberately not used to find it. Every source spells the title
    // differently — Apple sends "86--EIGHTY-SIX", the shelf holds "86―EIGHTY-SIX", Open
    // Library has both — so a prefix match would fail on exactly the series that most needs
    // this. Searching for the known number is enough and cannot be tripped by punctuation.
    private static string? AfterVolumeMarker(string recordTitle, int volumeNumber)
    {
        // The bare number is the fallback, never the first thing tried: where a record says
        // "Vol." it is unambiguous, and where it does not the number is all there is. Real:
        // Open Library titles this shelf's own editions "That Time I Got Reincarnated As a
        // Slime 16", where the number is the only thing separating series from subtitle.
        var marker = FirstMarkerFor(VolumeMarkerPattern(), recordTitle, volumeNumber)
                     ?? FirstMarkerFor(BareNumberPattern(), recordTitle, volumeNumber);

        return marker is null ? null : recordTitle[(marker.Index + marker.Length)..];
    }

    /// <summary>The leftmost match whose captured number is this volume's.</summary>
    private static Match? FirstMarkerFor(Regex pattern, string recordTitle, int volumeNumber)
    {
        foreach (Match match in pattern.Matches(recordTitle))
        {
            if (int.TryParse(
                    match.Groups["number"].ValueSpan, CultureInfo.InvariantCulture, out var number)
                && number == volumeNumber)
            {
                return match;
            }
        }

        return null;
    }

    /// <summary>
    /// The subtitle as it should be stored, or empty where nothing is left of it.
    /// </summary>
    private static string Tidy(string subtitle)
    {
        var tidied = LeadingSeparatorPattern().Replace(subtitle, string.Empty);

        // Looped, because a record can trail more than one: "…, Vol. 14 (light Novel)
        // (manga)" is not a shape this shelf has seen, but one pass would leave half of it
        // behind if it ever did.
        while (TrailingMediumMarkerPattern().Match(tidied) is { Success: true } marker)
        {
            tidied = tidied[..marker.Index];
        }

        return tidied.Trim();
    }

    /// <summary>
    /// A volume marker carrying a number: <c>, Vol. 3:</c>, <c>Vol. 17</c>, <c>Volume 3</c>.
    /// </summary>
    // Every spelling seen live across both sources. The leading comma is optional because
    // Apple writes "…, Vol. 8 (light novel)" and Open Library writes "… Volume 2: The Dark
    // Warrior"; the word boundary after the number is what stops "Vol. 12" from answering for
    // volume 1 and taking "2: …" as the subtitle.
    [GeneratedRegex(
        @"(?:,\s*)?\bvol(?:ume)?\.?\s*0*(?<number>\d{1,4})\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex VolumeMarkerPattern();

    /// <summary>A number standing on its own, with no volume word in front of it.</summary>
    // Refused where a digit or a comma follows, which is what keeps the rule off the
    // thousands separator in "Saving 80,000 Gold in Another World" — a real series on a
    // shelf like this one, and one whose title would otherwise be cut at "80".
    [GeneratedRegex(
        @"(?<![\p{L}\p{N}])0*(?<number>\d{1,4})(?![\p{N},])",
        RegexOptions.CultureInvariant)]
    private static partial Regex BareNumberPattern();

    /// <summary>The punctuation a source puts between the number and the subtitle.</summary>
    [GeneratedRegex(@"^[\s:;,\-–—]+", RegexOptions.CultureInvariant)]
    private static partial Regex LeadingSeparatorPattern();

    /// <summary>
    /// The medium marker, where it trails the subtitle.
    /// </summary>
    // It is the record's statement of which medium it is, which the matcher upstream needs
    // and a caption does not: the series already knows its own format, and "(light novel)"
    // under every tile of a light novel says nothing.
    [GeneratedRegex(
        @"\s*\((?:light\s+novel|manga)\)\s*$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex TrailingMediumMarkerPattern();
}
