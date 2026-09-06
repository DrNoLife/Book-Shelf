namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// What a volume is still missing, and therefore whether a source is worth a request.
/// </summary>
// One home for these, because both enrichment passes ask the question and a disagreement
// between them would be silent: the catalogue pass would keep asking about a volume the
// by-ISBN pass has decided is finished, every night, forever.
public static class VolumeGaps
{
    /// <summary>Whether any source is still worth a request for this volume at all.</summary>
    public static bool NeedsAnything(Volume volume)
    {
        ArgumentNullException.ThrowIfNull(volume);

        return volume.ReleaseDate is null
            || volume.CoverObjectKey is null
            || TitleUnresolved(volume)
            || volume.Description is null;
    }

    /// <summary>
    /// Whether any source has named this volume yet.
    /// </summary>
    // The distinction Volume.Title's three states exist for: an empty title is an answer —
    // "named, and it has no subtitle" — and asking again would spend a request a night on a
    // question already settled. Only null is a gap.
    public static bool TitleUnresolved(Volume volume)
    {
        ArgumentNullException.ThrowIfNull(volume);

        return volume.Title is null;
    }

    /// <summary>
    /// Whether this volume is worth a request to the by-ISBN source. Silent about the ones
    /// that are not.
    /// </summary>
    // Three reasons to skip, and none of them is an error:
    //
    // - No ISBN. The seam is keyed on ISBN because that is the only key that works; a
    //   volume without one has nothing to ask about. 41% of the owned shelf, and the
    //   discovery command is the answer to it, not this.
    // - A Japanese-market ISBN. It resolves to a Japanese-market edition, whose date is
    //   the Japanese release date and whose art is the Japanese cover — both wrong on an
    //   English shelf entry. Checked from the ISBN rather than from the record, because
    //   the record may state no language at all: Slime volume 1's Japanese ISBN resolves
    //   to a French edition with an empty `languages` field.
    // - Already complete. A volume with a date, a cover and a title has nothing left to
    //   learn, so a nightly run does not re-ask. Incomplete ones are re-asked, which is
    //   deliberate: Open Library's catalogue grows, and today's missing record is next
    //   quarter's hit.
    //
    // Description is deliberately absent from that last list, unlike NeedsAnything's.
    // Open Library carried a blurb for 0 of this shelf's 15 sampled ISBNs, so a volume
    // whose only gap is its description would be re-asked every night forever for a field
    // the source has never once supplied — sixty budgeted lookups a night spent on a
    // certainty.
    public static bool WorthAskingByIsbn(Volume volume)
    {
        ArgumentNullException.ThrowIfNull(volume);

        return volume.Isbn is not null
            && !Isbn.IsJapaneseEdition(volume.Isbn)
            && (volume.ReleaseDate is null || volume.CoverObjectKey is null || TitleUnresolved(volume));
    }
}
