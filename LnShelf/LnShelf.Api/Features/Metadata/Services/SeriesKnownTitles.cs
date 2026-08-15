namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// The titles a series is known by, reduced to the ones a US storefront could conceivably
/// be asked about.
/// </summary>
// One place for the hygiene both ends of the arbitration need: the mappers, which decide
// what <see cref="Series.AlternativeTitles"/> is worth storing, and the enrichment pass,
// which decides what order to ask them in. Both do the same three things — drop the titles
// no US store can match, collapse the ones that are the same title twice, and keep the
// list short — and a second copy of that would drift from this one.
public static class SeriesKnownTitles
{
    /// <summary>
    /// How many alternatives are worth keeping for one series.
    /// </summary>
    // Each one is a potential store request: the pass tries them in turn until the store
    // recognises one, so a series with twenty alternatives would be twenty requests against
    // a free API on the night it is added. Eight covers every real bag on this shelf —
    // MangaUpdates' longest, Slime's, holds three once the non-Latin entries are out.
    public const int MaximumAlternatives = 8;

    /// <summary>
    /// Every other title a source knows the series by, in the source's own order.
    /// </summary>
    /// <param name="titleEnglish">
    /// The title the shelf already stores, which is what these are alternatives *to* — a
    /// candidate repeating it is dropped rather than stored twice.
    /// </param>
    public static string[] Alternatives(string titleEnglish, IEnumerable<string?> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        return [.. Keep(candidates, [TitleNormalizer.Normalize(titleEnglish)], MaximumAlternatives)];
    }

    /// <summary>
    /// The same list as a store would be asked it: order preserved, repeats and
    /// unmatchable scripts gone.
    /// </summary>
    // Uncapped, unlike <see cref="Alternatives"/>: what reaches here has already been
    // capped on the way into the column.
    public static string[] Askable(IEnumerable<string?> titles)
    {
        ArgumentNullException.ThrowIfNull(titles);

        return [.. Keep(titles, [], int.MaxValue)];
    }

    /// <summary>
    /// Whether most of the letters in a title are Basic Latin ones.
    /// </summary>
    // A script filter, and deliberately *not* an English-picking heuristic — the difference
    // is the whole design. Nothing here tries to decide which of "Isekai Goumon Hime",
    // "Isekai Goumon-hime" and "Torture Princess: Fremd Torturchen" is the English name;
    // every cheap rule that tries picks wrong on one of them, which is why the store
    // arbitrates instead. This only drops the titles a US storefront cannot match under any
    // circumstances — the kanji, Cyrillic, Arabic and Chinese entries MangaUpdates keeps in
    // the same unlabelled bag. Romaji stays, and so does German: both are worth a request.
    //
    // Majority rather than "every letter", because the mixed entries are real: "戰記 Deus lo
    // vult" is one of Youjo Senki's, and its Latin half is a title a store could carry.
    // Digits and punctuation are not letters and do not vote, so "86" and "86--EIGHTY-SIX"
    // both count as Latin.
    public static bool IsLatinScript(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        var letters = 0;
        var latinLetters = 0;

        foreach (var rune in title.EnumerateRunes())
        {
            if (!Rune.IsLetter(rune))
            {
                continue;
            }

            letters++;

            if (rune.Value <= LastBasicLatinCodePoint)
            {
                latinLetters++;
            }
        }

        // A title with no letters at all is digits and punctuation — "86" — which a store
        // reads exactly as well as this shelf does.
        return letters is 0 || latinLetters * 2 > letters;
    }

    private const int LastBasicLatinCodePoint = 0x7F;

    /// <param name="alreadySeen">
    /// Normalised titles that count as taken before the first candidate is looked at.
    /// </param>
    // Deduped on the normalised form rather than case-insensitively, which is stricter and
    // saves real requests: "Isekai Goumon Hime" and "Isekai Goumon-hime" differ only in
    // punctuation, and the catalogue source keys its cache on the normalised title — so the
    // two are one request whatever this does. Collapsing them here makes that visible
    // instead of spending an attempt to rediscover it.
    private static List<string> Keep(
        IEnumerable<string?> titles,
        HashSet<string> alreadySeen,
        int maximum)
    {
        var kept = new List<string>();

        foreach (var candidate in titles)
        {
            if (kept.Count == maximum)
            {
                break;
            }

            var title = candidate?.Trim();

            if (string.IsNullOrEmpty(title) || !IsLatinScript(title))
            {
                continue;
            }

            if (alreadySeen.Add(TitleNormalizer.Normalize(title)))
            {
                kept.Add(title);
            }
        }

        return kept;
    }
}
