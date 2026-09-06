using System.Text.RegularExpressions;

namespace LnShelf.Api.Features.Import.Services;

/// <summary>
/// Whether two credits name the same person, across the spellings a book export
/// and a metadata source disagree about.
/// </summary>
// The same person reaches the matcher as "Shinobu Shinotsuki" from one source and
// "Shinotsuki, Shinobu" or "Shinobu Shinotsuki." from another, and a romanisation
// may or may not keep its diacritics. Comparing the raw strings would answer "no"
// to almost every real pair, which for a tie-break means the tie is never broken —
// a silent failure rather than a wrong answer, and the harder kind to notice.
//
// Deliberately not TitleNormalizer: that one is tuned for series titles (it strips
// subtitles and edition words), and a person's name is not a title.
public static partial class PersonNameNormalizer
{
    /// <summary>
    /// Lower case, no diacritics, no punctuation, single-spaced. Empty for a name
    /// that is null, blank, or nothing but punctuation.
    /// </summary>
    public static string Normalize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        // FormD splits "ō" into "o" + a combining macron, so dropping the
        // non-spacing marks leaves the letter behind.
        var decomposed = name.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) is UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            // Letters and digits survive; a hyphen, a full stop and a comma become
            // a space, so "so-bin" and "so bin" are the same two words.
            builder.Append(char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : ' ');
        }

        return WhitespaceRunPattern().Replace(builder.ToString(), " ").Trim();
    }

    /// <summary>
    /// Whether two credits are the same person, regardless of which order the
    /// source wrote the name parts in.
    /// </summary>
    // "Zen, Carlo" and "Carlo Zen" are one author; Japanese credits arrive in both
    // orders depending on who romanised them, and neither source says which
    // convention it used. Comparing the sorted parts settles it without the shelf
    // having to guess which half is the family name.
    //
    // A missing name never matches — including another missing one. "We do not know
    // who drew either of these" is not evidence that one candidate is the right one.
    public static bool AreSamePerson(string? left, string? right)
    {
        var leftParts = NameParts(left);
        var rightParts = NameParts(right);

        return leftParts.Length > 0 && leftParts.SequenceEqual(rightParts, StringComparer.Ordinal);
    }

    private static string[] NameParts(string? name)
    {
        var parts = Normalize(name).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Array.Sort(parts, StringComparer.Ordinal);

        return parts;
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRunPattern();
}
