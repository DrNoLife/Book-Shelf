namespace LnShelf.Api.Features.Releases.Abstractions;

/// <summary>The per-series half of the notification setting (feature-spec §4.5): mute.</summary>
public interface ISeriesNotificationCommandService
{
    /// <returns>The refreshed series, or null when there is no such series.</returns>
    Task<SeriesDetailResponse?> SetMutedAsync(Guid seriesId, bool muted, CancellationToken cancellationToken);
}
