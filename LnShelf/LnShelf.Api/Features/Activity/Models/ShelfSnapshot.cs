namespace LnShelf.Api.Features.Activity.Models;

/// <summary>A series as it stood before a refresh, for <c>ShelfEventDiff</c>.</summary>
/// <param name="Volumes">Every volume the series had, by id.</param>
/// <param name="ReleaseSourceVolumeCount">The release source's count before the refresh.</param>
/// <param name="HighestNumber">The highest volume number tracked before the refresh.</param>
/// <param name="PreviousSyncDay">The local calendar day of the last sync.</param>
/// <param name="MangaUpdatesId">The release-source record the series was linked to.</param>
public sealed record ShelfSnapshot(
    IReadOnlyDictionary<Guid, VolumeSnapshot> Volumes,
    int? ReleaseSourceVolumeCount,
    decimal? HighestNumber,
    DateOnly PreviousSyncDay,
    long? MangaUpdatesId);

/// <summary>One volume as it stood before a refresh.</summary>
public sealed record VolumeSnapshot(decimal Number, bool IsReleased, DateOnly? ReleaseDate);
