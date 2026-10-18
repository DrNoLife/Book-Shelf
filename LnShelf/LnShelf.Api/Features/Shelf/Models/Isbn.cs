namespace LnShelf.Api.Features.Shelf.Models;

/// <summary>
/// The one place an ISBN is cleaned up and read. <see cref="Volume.Isbn"/> is the
/// column, so the rules about what may go into it live beside it.
/// </summary>
// Three slices touch the value — Import carries them forward from an export,
// Metadata looks editions up by them, Shelf stores them — and none of the three
// should get its own opinion about hyphens.
public static class Isbn
{
    /// <summary>The column's width, which is also the widest form worth storing.</summary>
    public const int MaximumLength = 32;

    /// <summary>
    /// The digits only, or null when the value is blank or is not an ISBN at all.
    /// </summary>
    // Exports are not careful: StoryGraph writes bare digits, a hand-typed value
    // arrives as 978-1-9753-0117-0, and the "ISBN/UID" column is also where
    // StoryGraph puts its own internal ids for books that have no ISBN. Anything
    // that is not 10 or 13 characters of ISBN alphabet is refused rather than
    // stored, because a lookup by it would only ever 404.
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var digits = new StringBuilder(13);

        foreach (var character in value)
        {
            if (char.IsAsciiDigit(character))
            {
                digits.Append(character);
            }
            else if (character is 'X' or 'x')
            {
                // The ISBN-10 check digit, and only ever the last one.
                digits.Append('X');
            }
            else if (character is not ('-' or ' ' or '_'))
            {
                return null;
            }
        }

        var normalized = digits.ToString();

        return normalized.Length is 10 or 13 ? normalized : null;
    }

    /// <summary>
    /// The 13-digit form, or null when the value is not an ISBN. An ISBN-10 gains the 978
    /// prefix and a recomputed check digit.
    /// </summary>
    // For comparing two ISBNs written by different hands: an older encyclopedia row may give the
    // 10-digit form of the very edition the shelf holds as 13 digits, and they are one book.
    public static string? ToIsbn13(string? value)
    {
        var normalized = Normalize(value);

        if (normalized is not { Length: 10 })
        {
            return normalized;
        }

        var body = "978" + normalized[..9];
        var sum = 0;

        for (var index = 0; index < body.Length; index++)
        {
            var digit = body[index] - '0';
            sum += index % 2 == 0 ? digit : digit * 3;
        }

        return body + (char)('0' + ((10 - (sum % 10)) % 10));
    }

    /// <summary>
    /// Whether this identifies a Japanese-market edition — registration group 4.
    /// </summary>
    // Needed because one volume can arrive twice, once per edition: the real export
    // lists Slime volume 1 only under its Japanese title. The shelf tracks English
    // editions, so when there is a choice the Japanese one is the loser.
    public static bool IsJapaneseEdition(string? isbn)
    {
        var normalized = Normalize(isbn);

        return normalized is not null
            && (normalized.StartsWith("9784", StringComparison.Ordinal)
                || normalized.StartsWith("9794", StringComparison.Ordinal)
                || (normalized.Length is 10 && normalized[0] is '4'));
    }
}
