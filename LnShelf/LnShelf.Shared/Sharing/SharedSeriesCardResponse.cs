using LnShelf.Shared.Shelf;

namespace LnShelf.Shared.Sharing;

/// <summary>
/// One series on a shared shelf.
/// </summary>
// No series id, no ownership, no dates, no notes: only what the page draws. Adding a field
// here makes it public, so SharedShelfContractTests lists the allowed set.
public sealed record SharedSeriesCardResponse
{
    public required string TitleEnglish { get; init; }
    public string? TitleRomaji { get; init; }
    public required Format Format { get; init; }
    public required PublicationStatus PublicationStatus { get; init; }
    public required TrackingStatus TrackingStatus { get; init; }

    /// <summary>
    /// The series' own art, for <c>/api/shared/{token}/covers/{key}</c>. Never a volume's
    /// cover or an upload. Null renders the placeholder.
    /// </summary>
    public string? CoverObjectKey { get; init; }

    /// <summary>Volumes read.</summary>
    public required int Read { get; init; }

    /// <summary>
    /// Volumes out: dated in the past, released per the source, or read. Never "owned",
    /// which would let the count give away what the owner has bought.
    /// </summary>
    public required int Out { get; init; }
}
