namespace LnShelf.Api.Features.Changelog.Models;

/// <summary>
/// One released version and what changed in it, exactly as <see cref="Services.ChangelogCatalog"/>
/// states it, grouped into the three categories the catalog documents. Any of them may be empty.
/// </summary>
public sealed record ChangelogEntry(
    string Version,
    DateOnly Date,
    string Title,
    IReadOnlyList<string> Features,
    IReadOnlyList<string> Improvements,
    IReadOnlyList<string> Fixes);
