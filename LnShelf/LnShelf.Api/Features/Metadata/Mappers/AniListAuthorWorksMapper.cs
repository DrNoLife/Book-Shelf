namespace LnShelf.Api.Features.Metadata.Mappers;

/// <summary>
/// The author's other works out of the <c>AuthorWorks</c> query: the first staff edge that
/// credits an author, and the works that person wrote.
/// </summary>
public static class AniListAuthorWorksMapper
{
    /// <summary>
    /// The adaptation credit. A light novelist is "Story" on their novels and "Original Story"
    /// on the manga drawn from them, and the manga is exactly what a reader of the novel may
    /// want next — so this counts as writing here, though <see cref="AniListStaffMapper"/>
    /// does not read it as the author of the series it sits on.
    /// </summary>
    private const string AdaptationCredit = "Original Story";

    /// <param name="media">The series the query was asked about; it is left out of its own list.</param>
    public static AuthorWorks ToAuthorWorks(AniListMedia? media)
    {
        // The first author credit, in AniList's relevance order — the same person
        // FindAuthor names on the series.
        var author = media?.Staff?.Edges?
            .FirstOrDefault(edge => AniListStaffMapper.IsAuthorRole(edge.Role) && edge.Node is not null)?
            .Node;

        if (media is null || author is null)
        {
            return AuthorWorks.None;
        }

        var works = new List<AuthorWork>();
        var seen = new HashSet<int> { media.Id };

        foreach (var edge in author.StaffMedia?.Edges ?? [])
        {
            // Without the role filter the list fills with works the person only illustrated
            // or translated, which are not "more by" them.
            if (!IsWritingCredit(edge.StaffRole) || edge.Node is not { } work || !seen.Add(work.Id))
            {
                continue;
            }

            // Untracked formats are skipped, and so is an entry with no title at all, which
            // would otherwise be a tile with nothing to call it.
            if (AniListMediaMapper.ToFormat(work.Format) is not { } format || TitleOf(work) is not { } title)
            {
                continue;
            }

            works.Add(new AuthorWork
            {
                AniListId = work.Id,
                Title = title,
                Format = format,
                CoverImageUrl = work.CoverImage?.Large.NullIfBlank(),
            });
        }

        return new AuthorWorks(author.Name?.Full.NullIfBlank(), works);
    }

    /// <summary>The English title, or romaji then native where AniList has none.</summary>
    private static string? TitleOf(AniListMedia work) =>
        work.Title?.English.NullIfBlank()
        ?? work.Title?.Romaji.NullIfBlank()
        ?? work.Title?.Native.NullIfBlank();

    private static bool IsWritingCredit(string? role) =>
        AniListStaffMapper.IsAuthorRole(role)
        || (role?.Trim().StartsWith(AdaptationCredit, StringComparison.OrdinalIgnoreCase) ?? false);
}
