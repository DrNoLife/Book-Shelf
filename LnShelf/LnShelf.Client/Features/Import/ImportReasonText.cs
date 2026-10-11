namespace LnShelf.Client.Features.Import;

/// <summary>
/// What is actually uncertain about a series in the review queue, in a sentence.
/// </summary>
// "Review this" without saying what is uncertain is not a question anyone can answer. The
// format case is the common one for a book export and the only one a person can resolve
// instantly, so it gets the plainest sentence. Shared by the compact review row and the full
// card, which print the same reason.
public static class ImportReasonText
{
    public static string? For(ImportGroupResponse group)
    {
        ArgumentNullException.ThrowIfNull(group);

        return group.Reason switch
        {
            ImportReviewReason.FormatAmbiguous =>
                "A light novel and a manga share this title — only you know which you read.",
            ImportReviewReason.AmbiguousCandidates =>
                "Two candidates scored the same on title alone.",
            ImportReviewReason.LowConfidence =>
                $"Closest title only scored {group.MatchScore}.",
            ImportReviewReason.NoCandidate =>
                "AniList has nothing by this name.",
            _ => null,
        };
    }
}
