namespace LnShelf.Client.Features.Recommendations;

/// <summary>Edits a loaded For you page in place, so a dismissal needs no second fetch.</summary>
public static class ForYouRows
{
    /// <summary>Removes the work from every row and drops rows left empty.</summary>
    public static ForYouResponse Without(ForYouResponse response, int aniListId) =>
        response with
        {
            Rows =
            [
                .. response.Rows
                    .Select(row => row with { Works = [.. row.Works.Where(work => work.AniListId != aniListId)] })
                    .Where(row => row.Works.Count > 0),
            ],
        };
}
