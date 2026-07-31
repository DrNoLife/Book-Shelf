namespace LnShelf.Api.Features.Metadata.Models.OpenLibrary;

/// <summary>
/// The Open Library wire shapes, restricted to the fields the shelf reads.
/// </summary>
// Dirty boundary models: classes, settable members, permissive nullability
// (CLAUDE.md — Types). Mapped to clean records in Mappers/ and never travelling
// further inward.
public sealed class OpenLibrarySearchResponse
{
    public int NumFound { get; set; }

    public List<OpenLibrarySearchDocument>? Docs { get; set; }
}

/// <summary>
/// One search hit — a *work*, not an edition.
/// </summary>
// Its Title is deliberately never read. A search groups every edition of a work into
// one hit whose title is whichever edition Open Library considers primary, which for
// this shelf's series is usually the Japanese one; the language filter cannot help,
// because the hit's language array is the union across editions
// (both measured: .research/volume-enrichment/e-isbn-first-pipeline.md). The only
// useful field here is the ISBN list, and every candidate in it has to be verified
// one edition at a time.
public sealed class OpenLibrarySearchDocument
{
    public string? Key { get; set; }

    /// <summary>Every edition's ISBN, in no particular order and in no particular market.</summary>
    public List<string>? Isbn { get; set; }
}

/// <summary>
/// One edition, from <c>GET /isbn/{isbn}.json</c> — the answer that is actually
/// unambiguous.
/// </summary>
public sealed class OpenLibraryEdition
{
    /// <summary>Open Library's own key, e.g. <c>/books/OL35982653M</c>.</summary>
    public string? Key { get; set; }

    public string? Title { get; set; }

    /// <summary>
    /// The title including the volume part. Read in preference to
    /// <see cref="Title"/>, which drops it for some publishers.
    /// </summary>
    // Measured: reading full_title takes per-volume title cleanliness from 87.5% to
    // 96% — Torture Princess' editions are titled just "Torture Princess" but their
    // full_title carries the volume.
    public string? FullTitle { get; set; }

    public string? Subtitle { get; set; }

    /// <summary>
    /// Free text, and genuinely free: "2017-01-31", "Apr 24, 2018", "2022".
    /// </summary>
    public string? PublishDate { get; set; }

    public List<string>? Publishers { get; set; }

    /// <summary>Cover image ids; null or empty for a digital-only edition.</summary>
    public List<int>? Covers { get; set; }

    /// <summary>References like <c>/languages/eng</c>. Absent on plenty of records.</summary>
    public List<OpenLibraryKeyReference>? Languages { get; set; }
}

/// <summary>Open Library links to everything by key rather than by value.</summary>
public sealed class OpenLibraryKeyReference
{
    public string? Key { get; set; }
}
