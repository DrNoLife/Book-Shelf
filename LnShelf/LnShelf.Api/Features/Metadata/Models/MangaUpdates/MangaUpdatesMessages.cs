namespace LnShelf.Api.Features.Metadata.Models.MangaUpdates;

/// <summary>
/// The MangaUpdates v1 wire shapes, restricted to the fields the shelf reads.
/// </summary>
// Dirty boundary models: classes, settable members, permissive nullability
// (CLAUDE.md — Types). Mapped to clean records in Mappers/ and never travelling
// further inward.
public sealed class MangaUpdatesSearchRequest
{
    public string Search { get; set; } = string.Empty;

    /// <summary>MangaUpdates' own medium words — "Novel", "Manga".</summary>
    public List<string> Type { get; set; } = [];

    public int Perpage { get; set; } = 5;
    public int Page { get; set; } = 1;
}

/// <inheritdoc cref="MangaUpdatesSearchRequest"/>
public sealed class MangaUpdatesSearchResponse
{
    public int TotalHits { get; set; }
    public List<MangaUpdatesSearchResult>? Results { get; set; }
}

/// <inheritdoc cref="MangaUpdatesSearchRequest"/>
public sealed class MangaUpdatesSearchResult
{
    public MangaUpdatesSeriesRecord? Record { get; set; }
}

/// <summary>
/// A series record. The same shape comes back from search and from
/// <c>/series/{id}</c>, except that search omits <c>associated</c> — which is
/// exactly the field matching needs.
/// </summary>
public sealed class MangaUpdatesSeriesRecord
{
    public long SeriesId { get; set; }
    public string? Title { get; set; }

    /// <summary>"Novel", "Manga", "Doujinshi", "Artbook"…</summary>
    public string? Type { get; set; }

    /// <summary>A string in the payload, not a number.</summary>
    public string? Year { get; set; }

    /// <summary>
    /// Free text, and genuinely free: "14 Volumes (Ongoing)", "23 LN Volumes +
    /// 2 SS Volumes (Complete)", "33 Volumes (Complete) \n\nPart 1: 3 Volumes…".
    /// </summary>
    public string? Status { get; set; }

    public bool Completed { get; set; }
    public bool Licensed { get; set; }

    public List<MangaUpdatesAssociatedTitle>? Associated { get; set; }
}

/// <inheritdoc cref="MangaUpdatesSeriesRecord"/>
public sealed class MangaUpdatesAssociatedTitle
{
    public string? Title { get; set; }
}
