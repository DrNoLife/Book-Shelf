namespace LnShelf.Api.Features.Metadata.Models;

/// <summary>
/// One record from a store's catalogue, as <see cref="IVolumeCatalogueSource"/> returns it.
/// </summary>
// Deliberately not a VolumeEnrichment. That record answers "what does this ISBN's edition
// say", so it carries an ISBN's worth of certainty — a language, a publisher, a raw publish
// string — and its caller already knows which volume it asked about. A catalogue record is
// the opposite situation: nobody asked about a volume, so the record has to say which one
// it is, and <see cref="Title"/> is the only evidence of that.
public sealed record CatalogueVolume
{
    /// <summary>The store's own identity for the record, for logs.</summary>
    public required string SourceRecordKey { get; init; }

    /// <summary>
    /// The store's own title string, verbatim.
    /// </summary>
    // Required, unlike every other field, because it is load-bearing rather than
    // decorative: VolumeEditionMatcher reads it to decide which volume this is and whether
    // the record is even the right medium. A record with no title cannot be placed, so a
    // source that cannot supply one has nothing to return.
    public required string Title { get; init; }

    /// <summary>The volume's blurb, already plain text. Null when the store has none.</summary>
    public string? Description { get; init; }

    /// <summary>
    /// The storefront's release date, which is genuinely present for future volumes too.
    /// </summary>
    // A store lists preorders, so a date here can be in the future in the ordinary course
    // of business. That is data, not an error — the conflict rules decide what may be
    // written from it.
    public DateOnly? ReleaseDate { get; init; }

    /// <summary>
    /// The store's cover art at the largest size it serves. Null when it has none, which
    /// includes the placeholder a store shows for a book whose art is not ready yet.
    /// </summary>
    public Uri? ArtworkUrl { get; init; }

    /// <summary>
    /// Whether the source looked for its placeholder art and ruled it out, so
    /// <see cref="ArtworkUrl"/> is the real cover even on a book that is not out yet.
    /// </summary>
    // False when the source cannot tell — an artwork URL of a shape it does not recognise —
    // which leaves VolumeFactWriter's older rule to decide: no art cached before the release date.
    public bool ArtworkScreened { get; init; }

    /// <summary>
    /// The store's own genre names for the record, verbatim ("Manga", "Comics &amp; Graphic
    /// Novels"). Empty when it states none.
    /// </summary>
    // Read only as medium evidence for manga (VolumeEditionMatcher.ConfirmsMedium), where Viz
    // titles carry no marker of their own.
    public IReadOnlyList<string> Genres { get; init; } = [];
}
