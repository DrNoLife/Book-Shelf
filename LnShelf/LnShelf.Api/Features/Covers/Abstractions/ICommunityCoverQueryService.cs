namespace LnShelf.Api.Features.Covers.Abstractions;

/// <summary>
/// What other users have shared for the signed-in user's volumes (feature-spec §4.3).
/// </summary>
public interface ICommunityCoverQueryService
{
    /// <returns>Null when the volume is not on the user's shelf.</returns>
    Task<CommunityCoversResponse?> ListForVolumeAsync(Guid volumeId, CancellationToken cancellationToken);

    /// <summary>The signed-in user's own sharing setting.</summary>
    Task<CoverSharingResponse> GetSharingAsync(CancellationToken cancellationToken);

    /// <returns>Null when no published cover has this hash, or its image is gone.</returns>
    Task<CoverContent?> ReadImageAsync(string contentHash, CancellationToken cancellationToken);

    /// <summary>
    /// The image of a cover another user shared that matches this volume.
    /// </summary>
    /// <returns>Null when nobody else shares a cover with this hash for the volume.</returns>
    Task<CoverContent?> FindMatchingAsync(
        Series series,
        Volume volume,
        string contentHash,
        CancellationToken cancellationToken);
}
