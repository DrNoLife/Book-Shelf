namespace LnShelf.Api.Features.Changelog.Models;

/// <summary>
/// One released version and what changed in it, exactly as <see cref="Services.ChangelogCatalog"/>
/// states it.
/// </summary>
public sealed record ChangelogEntry(string Version, DateOnly Date, string Title, IReadOnlyList<string> Changes);
