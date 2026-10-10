using System.Text;

namespace LnShelf.Client.Features.Search;

/// <summary>
/// Folds a title down to the form two spellings of it agree in, for the search page's grouping.
/// </summary>
// The client cannot reference the Api's TitleNormalizer (blazor.md keeps server code out of
// the bundle), so the folding rules it needs are written here: the same accent drop,
// punctuation-as-separator and leading-article rule the import's matcher compares titles by.
public static class SearchTitleNormalizer
{
    /// <summary>
    /// Lower case, accents dropped, every run of punctuation a single space, a leading "the"
    /// removed: the form two spellings of one title agree in.
    /// </summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        // Canonical decomposition separates an accent from its letter so it can be dropped.
        // Not the compatibility form the Api's normaliser uses: the browser runtime does not
        // support it, and full-width digits are not what tells two search hits apart.
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var rune in decomposed.EnumerateRunes())
        {
            // The voiced sound marks are kept: without them バ and ハ would be one letter.
            var isJapaneseSoundMark = rune.Value is 0x3099 or 0x309A;

            if (Rune.GetUnicodeCategory(rune) is UnicodeCategory.NonSpacingMark && !isJapaneseSoundMark)
            {
                continue;
            }

            builder.Append(Rune.IsLetterOrDigit(rune) ? Rune.ToLowerInvariant(rune).ToString() : " ");
        }

        var collapsed = string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));

        // Sources disagree about the leading article constantly; anywhere else it carries meaning.
        return collapsed.StartsWith("the ", StringComparison.Ordinal) ? collapsed[4..] : collapsed;
    }
}
