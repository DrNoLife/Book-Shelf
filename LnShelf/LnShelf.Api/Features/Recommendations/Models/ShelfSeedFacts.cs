namespace LnShelf.Api.Features.Recommendations.Models;

/// <summary>
/// What the shelf says about one series, as far as choosing a For you seed needs it.
/// </summary>
/// <param name="ExternalId">The catalogue id (<c>Series.AniListId</c>); null for a series the catalogue never matched.</param>
/// <param name="ReadVolumes">How many volumes are marked Read.</param>
/// <param name="HasChapterProgress">Whether the reader recorded a last chapter read.</param>
/// <param name="LastReadAt">The series' own last-read date.</param>
/// <param name="NewestVolumeReadAt">The newest read date on any of its volumes.</param>
/// <param name="StartedAt">When the reader started it.</param>
public sealed record ShelfSeedFacts(
    Guid SeriesId,
    int? ExternalId,
    string Title,
    Format Format,
    TrackingStatus Status,
    int ReadVolumes,
    bool HasChapterProgress,
    DateOnly? LastReadAt,
    DateOnly? NewestVolumeReadAt,
    DateOnly? StartedAt);
