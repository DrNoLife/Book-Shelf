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
        // Every occurrence, wherever it sits, rather than only a trailing one. Both positions
        // are real and the shelf has both: Apple trails the marker ("…, Vol. 14 (light
        // novel)"), while Open Library puts it between the number and the subtitle ("Saga of
        // Tanya the Evil, Vol. 8 (light Novel): In Omnia Paratus"), which leaves it at the
        // *front* of what this method is given — where an end-anchored rule cannot reach it,
        // and where it was reaching captions as "(light Novel): In Omnia Paratus".
        var tidied = MediumMarkerPattern().Replace(subtitle, string.Empty);

        // After the marker rather than before it: taking the marker out is what exposes the
        // separator the source put between the number and the subtitle.
        tidied = LeadingSeparatorPattern().Replace(tidied, string.Empty).Trim();

        return IsSingleParenthetical(tidied) ? string.Empty : tidied;
    }

    /// <summary>
    /// Whether all that is left is one bracketed aside, which is an annotation and not a
    /// subtitle.
    /// </summary>
    // Real, and the reason this exists: Open Library titles this shelf's Slime volume 3
    // "…, Vol. 3 (light novel) (That Time I Got Reincarnated as a Slime (light novel))", whose
    // leftover is the series name over again in brackets. A caption says what this volume is
    // called, and a bracketed repeat of the series it belongs to is not that — the honest
    // answer is the same "no subtitle" every other Slime volume gets.
    //
    // Only the wholly-wrapped case is refused, which is what the closing bracket landing last
    // tests for. "Run Through the Battlefront (Finish)" is a subtitle that happens to carry a
    // bracket and keeps it, and "(Start) and (Finish)" closes early and is left alone too.
    private static bool IsSingleParenthetical(string subtitle) =>
        subtitle.StartsWith('(') && subtitle.IndexOf(')') == subtitle.Length - 1;

    /// <summary>
    /// A volume marker carrying a number: <c>, Vol. 3:</c>, <c>Vol. 17</c>, <c>Volume 3</c>.
    /// </summary>
    // Every spelling seen live across both sources. The leading comma is optional because
    // Apple writes "…, Vol. 8 (light novel)" and Open Library writes "… Volume 2: The Dark
    // Warrior"; the word boundary after the number is what stops "Vol. 12" from answering for
    // volume 1 and taking "2: …" as the subtitle. The lookahead refuses a bonus volume's
    // "Vol. 7.5" (and Open Library's "Vol. 7. 5") as volume 7, which would leave ".5" as the
    // subtitle.
    [GeneratedRegex(
        @"(?:,\s*)?\bvol(?:ume)?\.?\s*0*(?<number>\d{1,4})\b(?!\.\s?\d)",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex VolumeMarkerPattern();

    /// <summary>A number standing on its own, with no volume word in front of it.</summary>
    // Refused where a digit or a comma follows, which is what keeps the rule off the
    // thousands separator in "Saving 80,000 Gold in Another World" — a real series on a
    // shelf like this one, and one whose title would otherwise be cut at "80". A following
    // ".5" is refused for the same reason as in the marker pattern: that is a bonus volume.
    [GeneratedRegex(
        @"(?<![\p{L}\p{N}])0*(?<number>\d{1,4})(?![\p{N},]|\.\s?\d)",
        RegexOptions.CultureInvariant)]
    private static partial Regex BareNumberPattern();

    /// <summary>The punctuation a source puts between the number and the subtitle.</summary>
    [GeneratedRegex(@"^[\s:;,\-–—]+", RegexOptions.CultureInvariant)]
    private static partial Regex LeadingSeparatorPattern();

    /// <summary>
    /// The medium marker, wherever in the leftover it sits.
    /// </summary>
    // It is the record's statement of which medium it is, which the matcher upstream needs
    // and a caption does not: the series already knows its own format, and "(light novel)"
    // under every tile of a light novel says nothing.
    //
    // Unanchored, so one pass takes every copy: a record can carry it twice ("…, Vol. 3
    // (light novel) (… (light novel))" is a real Open Library title), and the position it
    // appears in differs by source rather than being fixed.
    [GeneratedRegex(
        @"\s*\((?:light\s+novel|manga)\)",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex MediumMarkerPattern();
}
