using LnShelf.Shared.Shelf;

namespace LnShelf.Shared.Import;

/// <summary>
/// One candidate series the review queue offers, with everything needed to tell
/// it apart from its neighbour: cover, format, publication status, volume count.
/// </summary>
public sealed record ImportCandidateResponse
{
    public required int AniListId { get; init; }
    public required string TitleEnglish { get; init; }
    public string? TitleRomaji { get; init; }
    public string? TitleNative { get; init; }
    public required Format Format { get; init; }
    public required PublicationStatus PublicationStatus { get; init; }
    public int? VolumeCount { get; init; }
    public int? StartYear { get; init; }

    /// <summary>Locally cached — the browser never touches an external CDN.</summary>
    public string? CoverObjectKey { get; init; }

    /// <summary>Title similarity, 0–100. Printed so the ranking is not a mystery.</summary>
    public required int Score { get; init; }
}
