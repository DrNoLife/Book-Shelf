namespace LnShelf.Api.Features.Import.Models;

/// <summary>
/// One scored candidate for a group, stored at match time.
/// </summary>
// Persisted rather than re-queried so the review queue renders from the database:
// opening the page must not spend AniList requests, and the candidates a decision
// was made against must not change under the user between page loads.
public sealed class ImportCandidate
{
    public Guid Id { get; set; }
    public Guid ImportGroupId { get; set; }

    public int AniListId { get; set; }

    public string TitleEnglish { get; set; } = string.Empty;
    public string? TitleRomaji { get; set; }
    public string? TitleNative { get; set; }

    public Format Format { get; set; }
    public PublicationStatus PublicationStatus { get; set; }
    public int? VolumeCount { get; set; }
    public int? StartYear { get; set; }

    /// <summary>Cached at match time, so the queue renders covers offline.</summary>
    public string? CoverObjectKey { get; set; }

    public int Score { get; set; }

    /// <summary>0 is the best candidate.</summary>
    public int Rank { get; set; }
}
