namespace LnShelf.Api.Features.Covers.Abstractions;

/// <summary>
/// Publishes and withdraws the signed-in user's uploaded covers for other users to find
/// (feature-spec §4.3).
/// </summary>
public interface ICommunityCoverCommandService
{
    /// <summary>
    /// Turns sharing on, publishing every cover already uploaded for a catalogued volume, or
    /// off, withdrawing all of the user's published covers.
    /// </summary>
    Task<CoverSharingResponse> SetSharingAsync(bool enabled, CancellationToken cancellationToken);

    /// <summary>
    /// Brings the published row for one volume in step with its cover override, in the change
    /// tracker only: the caller's own SaveChanges writes it with the override change.
    /// </summary>
    /// <param name="series">The volume's series, for the ids and format the row snapshots.</param>
    /// <param name="objectKey">The volume's override object, or null when the override was removed.</param>
    /// <param name="contentHash">The hash of the bytes stored at <paramref name="objectKey"/>.</param>
    // Does nothing for a user who is not sharing and for a series no other user can match.
    Task StageForVolumeAsync(
        Series series,
        Volume volume,
        string? objectKey,
        string? contentHash,
        CancellationToken cancellationToken);

    /// <summary>Stops offering the user a shared cover. Hiding twice is the same as once.</summary>
    // The user's own cover is left alone, even when it is this very image.
    Task HideAsync(string contentHash, CancellationToken cancellationToken);

    /// <summary>Offers a hidden cover again. Unhiding one that is not hidden does nothing.</summary>
    Task UnhideAsync(string contentHash, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes every published row with this hash, whoever published it: owner moderation,
    /// reached only from the console.
    /// </summary>
    /// <returns>How many rows were deleted.</returns>
    Task<int> WithdrawAsync(string contentHash, CancellationToken cancellationToken);
}
