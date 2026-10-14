namespace LnShelf.Api.Features.Metadata.Models;

/// <summary>One English Wikipedia page's wikitext, under the title it was found at after redirects.</summary>
public sealed record WikipediaPage(string Title, string Wikitext);

/// <summary>
/// One <c>{{Graphic novel list}}</c> row of a Wikipedia page: a volume, its English edition's
/// ISBNs and the chapters it collects.
/// </summary>
/// <param name="VolumeNumber">Null when the row's number is not a number ("—", a template).</param>
/// <param name="LicensedIsbns">The English edition's ISBNs, as 13 digits. Empty before a licence.</param>
/// <param name="Chapters">The chapters the row lists, or null when it lists them without numbers.</param>
public sealed record GraphicNovelListEntry(
    decimal? VolumeNumber,
    IReadOnlyList<string> LicensedIsbns,
    IReadOnlyList<decimal>? Chapters);

/// <summary>
/// One table of a page: the rows under one <c>{{Graphic novel list/header}}</c>. A page about a
/// franchise holds several (Slime: the light novels, then each manga).
/// </summary>
public sealed record GraphicNovelListTable(IReadOnlyList<GraphicNovelListEntry> Entries);

/// <summary>A page and the one table on it whose ISBNs are this series' own.</summary>
public sealed record VerifiedChapterList(string PageTitle, GraphicNovelListTable Table);
