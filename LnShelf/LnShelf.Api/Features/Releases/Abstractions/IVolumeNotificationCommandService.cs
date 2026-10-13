namespace LnShelf.Api.Features.Releases.Abstractions;

/// <summary>The per-volume half of the notification setting: "notify me when dated".</summary>
public interface IVolumeNotificationCommandService
{
    /// <returns>False when there is no such volume.</returns>
    /// <exception cref="ConflictException">Turning it on for a volume that already has a date or is out.</exception>
    Task<bool> SetNotifyWhenDatedAsync(Guid volumeId, bool notifyWhenDated, CancellationToken cancellationToken);
}
