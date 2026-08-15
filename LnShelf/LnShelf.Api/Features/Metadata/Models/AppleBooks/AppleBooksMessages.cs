namespace LnShelf.Api.Features.Metadata.Models.AppleBooks;

/// <summary>
/// The iTunes Search wire shapes, restricted to the fields the shelf reads.
/// </summary>
// Dirty boundary models: classes, settable members, permissive nullability
// (CLAUDE.md — Types). Mapped to clean records in Mappers/ and never travelling further
// inward.
public sealed class AppleBooksSearchResponse
{
    public int ResultCount { get; set; }

    public List<AppleBooksSearchResult>? Results { get; set; }
}

/// <summary>
/// One store listing. The endpoint mixes media types into one array, so not every row
/// here is a book.
/// </summary>
public sealed class AppleBooksSearchResult
{
    /// <summary>
    /// Apple's own id for the listing — the stable half of the record.
    /// </summary>
    // Nullable because the array is not homogeneous: an audiobook row carries a
    // collectionId and no trackId at all.
    public long? TrackId { get; set; }

    /// <summary>
    /// The publisher's own title, volume part and medium marker included:
    /// "86--EIGHTY-SIX, Vol. 8 (light novel)".
    /// </summary>
    // The field the whole feature turns on. Apple's ebook titles carry the series name,
    // the volume number and the medium in one string, which is exactly the shape
    // VolumeEditionMatcher was written against.
    public string? TrackName { get; set; }

    /// <summary>The volume's own blurb, as HTML — a few hundred characters of it.</summary>
    public string? Description { get; set; }

    /// <summary>
    /// Full ISO timestamp, and genuinely forward-looking: a preorder carries its
    /// announced date.
    /// </summary>
    public string? ReleaseDate { get; set; }

    /// <summary>The cover thumbnail. Its size is a path segment, so a bigger one is a swap.</summary>
    public string? ArtworkUrl100 { get; set; }

    public string? ArtistName { get; set; }

    /// <summary>
    /// "ebook" for the rows the shelf wants.
    /// </summary>
    // Absent rather than different on an audiobook row, which carries `wrapperType`
    // instead — so the filter has to be "says ebook", not "does not say audiobook".
    public string? Kind { get; set; }
}
