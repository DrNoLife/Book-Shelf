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
    // Absent from search results entirely — see the class comment — so a search hit
    // knows neither the volume count nor whether publication has finished.
    public string? Status { get; set; }

    /// <summary>
    /// "Completely Scanlated?" on the site — the fan translation, not publication. Kept for
    /// the wire shape only; publication status is read from <see cref="Status"/>.
    /// </summary>
    public bool Completed { get; set; }

    public bool Licensed { get; set; }

    public List<MangaUpdatesAssociatedTitle>? Associated { get; set; }

    /// <summary>
    /// The synopsis. Present in search results as well as in the full record.
    /// </summary>
    // Not HTML, and not plain either: MangaUpdates stores wiki-ish markup, so a
    // description arrives with real newlines, `**bold**` and `[label](url)` links in it.
    // HtmlDescriptionSanitizer is still the right edge for it — that is what decodes the
    // entities and normalises the line structure — but it leaves the link syntax alone,
    // which is honest: the alternative is a markdown parser for a synopsis field.
    public string? Description { get; set; }

    public MangaUpdatesImage? Image { get; set; }

    public List<MangaUpdatesGenre>? Genres { get; set; }

    /// <summary>Absent from search results; only the full record carries it.</summary>
    public List<MangaUpdatesAuthor>? Authors { get; set; }

    /// <inheritdoc cref="Authors"/>
    public List<MangaUpdatesPublisher>? Publishers { get; set; }
}

/// <inheritdoc cref="MangaUpdatesSeriesRecord"/>
public sealed class MangaUpdatesAssociatedTitle
{
    public string? Title { get; set; }
}

/// <inheritdoc cref="MangaUpdatesSeriesRecord"/>
public sealed class MangaUpdatesImage
{
    public MangaUpdatesImageUrl? Url { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
}

/// <inheritdoc cref="MangaUpdatesSeriesRecord"/>
public sealed class MangaUpdatesImageUrl
{
    /// <summary>The full-size cover. <see cref="Thumb"/> is 160px wide and no use here.</summary>
    public string? Original { get; set; }

    public string? Thumb { get; set; }
}

/// <inheritdoc cref="MangaUpdatesSeriesRecord"/>
public sealed class MangaUpdatesGenre
{
    public string? Genre { get; set; }
}

/// <inheritdoc cref="MangaUpdatesSeriesRecord"/>
public sealed class MangaUpdatesAuthor
{
    /// <summary>Surname first and upper-cased: "AYASATO Keishi".</summary>
    public string? Name { get; set; }

    /// <summary>"Author" or "Artist" — which is what separates writer from illustrator.</summary>
    public string? Type { get; set; }
}

/// <inheritdoc cref="MangaUpdatesSeriesRecord"/>
public sealed class MangaUpdatesPublisher
{
    public string? PublisherName { get; set; }

    /// <summary>"Original" (the Japanese imprint) or "English".</summary>
    public string? Type { get; set; }

    /// <summary>
    /// The publisher's own edition in prose: "9 Volumes + 1 Extra - Complete", "11+3 Volumes;
    /// Complete", "4 Audiobooks; Ongoing". Empty or absent as often as not.
    /// </summary>
    public string? Notes { get; set; }
}
