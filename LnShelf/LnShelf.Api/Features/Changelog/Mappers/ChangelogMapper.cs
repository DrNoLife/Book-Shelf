namespace LnShelf.Api.Features.Changelog.Mappers;

/// <summary>
/// Turns the catalogue's entries into the changelog response.
/// </summary>
// Hand-written and living in the slice's Mappers/ folder rather than inline in the
// controller (CLAUDE.md — Mapping). The controller was the only mapper in the app that sat
// in a Controllers/ folder, which is exactly the "where does X live?" question the folder
// rule exists to answer.
public static class ChangelogMapper
{
    public static ChangelogResponse ToResponse(IReadOnlyList<ChangelogEntry> entries, string currentVersion)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return new ChangelogResponse
        {
            CurrentVersion = currentVersion,
            Entries = [.. entries.Select(ToEntry)],
        };
    }

    public static ChangelogEntryResponse ToEntry(ChangelogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return new ChangelogEntryResponse
        {
            Version = entry.Version,
            Date = entry.Date,
            Title = entry.Title,
            Changes = entry.Changes,
        };
    }
}
