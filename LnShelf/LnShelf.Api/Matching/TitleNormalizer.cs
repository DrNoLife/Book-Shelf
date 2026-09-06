namespace LnShelf.Api.Matching;

/// <summary>
/// Folds a title down to the form titles are compared in. Used by both the
/// grouping key and the scorer, so a group and its match agree on what "the same
/// title" means.
/// </summary>
public static class TitleNormalizer
{
    /// <returns>Lower-case, punctuation-free, single-spaced. Empty for no title.</returns>
    public static string Normalize(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }

        // Compatibility decomposition, not canonical: it folds full-width digits
        // and half-width katakana onto their ordinary forms, which is what makes
        // AniList's "８６―エイティシックス―" and the export's "86—EIGHTY-SIX"
        // comparable at all.
        var decomposed = title.Normalize(NormalizationForm.FormKD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var rune in decomposed.EnumerateRunes())
        {
            // The mark left behind by decomposing an accented letter — dropped, so
            // an é and an e are the same title.
            if (Rune.GetUnicodeCategory(rune) is UnicodeCategory.NonSpacingMark && !IsJapaneseSoundMark(rune))
            {
                continue;
            }

            if (Rune.IsLetterOrDigit(rune))
            {
                builder.Append(Rune.ToLowerInvariant(rune).ToString());
                continue;
            }

            // Everything else becomes a separator: dashes, colons, tildes,
            // brackets, quotes. This is the single rule that makes "86—EIGHTY-SIX"
            // (em dash), "86―EIGHTY-SIX" (horizontal bar) and "86--EIGHTY-SIX"
            // (two hyphens) one title — all three appear in one real export.
            builder.Append(' ');
        }

        return StripLeadingArticle(CollapseWhitespace(builder.ToString()));
    }

    /// <summary>Whitespace-separated words of a normalised title.</summary>
    public static string[] Tokenize(string normalizedTitle) =>
        string.IsNullOrEmpty(normalizedTitle)
            ? []
            : normalizedTitle.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// The voiced and semi-voiced sound marks, which decomposition separates from
    /// their kana.
    /// </summary>
    // They are kept, unlike Latin accents: dropping them turns オーバーロード into
    // オーハーロート, which is not a spelling variant but a different word. It
    // would still have matched AniList — both sides go through this function — but
    // it would quietly make ハ and バ the same letter.
    private static bool IsJapaneseSoundMark(Rune rune) => rune.Value is 0x3099 or 0x309A;

    private static string CollapseWhitespace(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    // Only the leading article, and only "the": sources disagree about it
    // constantly ("The Saga of Tanya the Evil" vs "Saga of Tanya the Evil"), while
    // an article anywhere else carries meaning.
    private static string StripLeadingArticle(string value) =>
        value.StartsWith("the ", StringComparison.Ordinal) ? value[4..] : value;
}
