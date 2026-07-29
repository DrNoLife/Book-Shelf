namespace LnShelf.Api.Features.Metadata.Mappers;

/// <summary>
/// Author and illustrator out of AniList's staff edges.
/// </summary>
// The role strings are freeform community text, which is why this is a prefix
// match and not an equality check: they arrive padded ("Story "), qualified
// ("Translator (English: vols 2, 4, 6-8)"), and a single mangaka is credited
// once as "Story & Art" — which is why that one name is correctly returned as
// both author and illustrator.
public static class AniListStaffMapper
{
    private static readonly string[] AuthorRolePrefixes = ["Story", "Original Creator"];
    private static readonly string[] IllustratorRolePrefixes = ["Art", "Illustration", "Character Design"];

    public static string? FindAuthor(AniListStaffConnection? staff) =>
        FindName(staff, AuthorRolePrefixes);

    public static string? FindIllustrator(AniListStaffConnection? staff)
    {
        var illustrator = FindName(staff, IllustratorRolePrefixes);
        if (illustrator is not null)
        {
            return illustrator;
        }

        // "Story & Art" starts with Story, so it never matches the prefixes
        // above — but it is exactly the case where author and illustrator are
        // one person.
        return FindName(staff, ["Story & Art", "Story and Art"]);
    }

    private static string? FindName(AniListStaffConnection? staff, string[] rolePrefixes)
    {
        foreach (var edge in staff?.Edges ?? [])
        {
            var role = edge.Role?.Trim();
            var name = edge.Node?.Name?.Full?.Trim();

            if (string.IsNullOrEmpty(role) || string.IsNullOrEmpty(name))
            {
                continue;
            }

            foreach (var prefix in rolePrefixes)
            {
                if (role.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return name;
                }
            }
        }

        return null;
    }
}
