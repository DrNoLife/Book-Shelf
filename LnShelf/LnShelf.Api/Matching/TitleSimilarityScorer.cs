namespace LnShelf.Api.Matching;

/// <summary>
/// Title similarity, 0–100. Deliberately explainable: the review queue prints
/// the number, so it has to mean something a person can argue with.
/// </summary>
public static class TitleSimilarityScorer
{
    public const int ExactScore = 100;

    /// <summary>Best score across a candidate's title variants.</summary>
    public static int BestScore(string? importTitle, params string?[] candidateTitles)
    {
        ArgumentNullException.ThrowIfNull(candidateTitles);

        var best = 0;
        foreach (var candidateTitle in candidateTitles)
        {
            best = Math.Max(best, Score(importTitle, candidateTitle));
        }

        return best;
    }

    public static int Score(string? importTitle, string? candidateTitle)
    {
        var left = TitleNormalizer.Normalize(importTitle);
        var right = TitleNormalizer.Normalize(candidateTitle);

        if (left.Length == 0 || right.Length == 0)
        {
            return 0;
        }

        if (string.Equals(left, right, StringComparison.Ordinal))
        {
            return ExactScore;
        }

        // The two measures fail in different directions, which is why the better of
        // them is taken rather than an average: edit distance handles punctuation
        // and spelling drift but punishes a candidate for carrying a long subtitle,
        // while token overlap ignores word order and length but scores nothing at
        // all for Japanese, which has no spaces to tokenise on.
        var characterRatio = LevenshteinRatio(left, right);
        var tokenRatio = DiceCoefficient(TitleNormalizer.Tokenize(left), TitleNormalizer.Tokenize(right));

        return (int)Math.Round(100 * Math.Max(characterRatio, tokenRatio));
    }

    private static double LevenshteinRatio(string left, string right)
    {
        var distance = LevenshteinDistance(left, right);
        var longest = Math.Max(left.Length, right.Length);

        return longest == 0 ? 1 : 1 - ((double)distance / longest);
    }

    /// <summary>Two-row Levenshtein — titles are short, so this is plenty.</summary>
    private static int LevenshteinDistance(string left, string right)
    {
        var previous = new int[right.Length + 1];
        var current = new int[right.Length + 1];

        for (var rightIndex = 0; rightIndex <= right.Length; rightIndex++)
        {
            previous[rightIndex] = rightIndex;
        }

        for (var leftIndex = 1; leftIndex <= left.Length; leftIndex++)
        {
            current[0] = leftIndex;

            for (var rightIndex = 1; rightIndex <= right.Length; rightIndex++)
            {
                var substitutionCost = left[leftIndex - 1] == right[rightIndex - 1] ? 0 : 1;

                current[rightIndex] = Math.Min(
                    Math.Min(current[rightIndex - 1] + 1, previous[rightIndex] + 1),
                    previous[rightIndex - 1] + substitutionCost);
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }

    /// <summary>Overlap of distinct words: 2·|A∩B| / (|A|+|B|).</summary>
    private static double DiceCoefficient(string[] left, string[] right)
    {
        if (left.Length == 0 || right.Length == 0)
        {
            return 0;
        }

        var leftSet = new HashSet<string>(left, StringComparer.Ordinal);
        var rightSet = new HashSet<string>(right, StringComparer.Ordinal);
        var shared = leftSet.Count(token => rightSet.Contains(token));

        return 2.0 * shared / (leftSet.Count + rightSet.Count);
    }
}
