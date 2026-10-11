namespace LnShelf.Client.Features.Import;

/// <summary>One answer a review row offers as a single tap: "Light novel" for this candidate.</summary>
public sealed record ImportQuickChoice(int AniListId, Format Format);

/// <summary>
/// The one-tap answers for a series waiting in the review queue (design: Import.dc's
/// "Light novel · Manga · Skip").
/// </summary>
// Only when the format alone tells the candidates apart — the common case for a book export,
// where a light novel and its manga share a title. Two candidates of one format (a series and
// its spin-off) are not something a format button can choose between, so such a row gets the
// full card with covers instead, and no quick answer is guessed for it.
public static class ImportQuickChoices
{
    public static IReadOnlyList<ImportQuickChoice> For(ImportGroupResponse group)
    {
        ArgumentNullException.ThrowIfNull(group);

        var candidates = group.Candidates;
        var formats = candidates.Select(candidate => candidate.Format).Distinct().Count();

        if (candidates.Count < 2 || formats != candidates.Count)
        {
            return [];
        }

        return
        [
            .. candidates
                .OrderBy(candidate => candidate.Format)
                .Select(candidate => new ImportQuickChoice(candidate.AniListId, candidate.Format)),
        ];
    }
}
