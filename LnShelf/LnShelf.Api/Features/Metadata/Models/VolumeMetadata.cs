namespace LnShelf.Api.Features.Metadata.Models;

/// <summary>
/// One volume as a metadata source describes it.
/// </summary>
// All three members exist because the seam has to carry what MangaUpdates will
// provide in phase 2, not only what AniList provides today: AniList publishes a
// volume *count*, so it fills Number and nothing else.
public sealed record VolumeMetadata
{
    public required int Number { get; init; }

    /// <summary>Null means announced with no date, or simply not known by this source.</summary>
    public DateOnly? ReleaseDate { get; init; }

    /// <summary>
    /// The source's own id for this volume, where it identifies volumes
    /// individually. AniList does not, so it stays null.
    /// </summary>
    public string? ExternalId { get; init; }
}
