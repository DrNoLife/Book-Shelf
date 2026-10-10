namespace LnShelf.Api.Features.Sharing.Abstractions;

/// <summary>
/// Changes the signed-in user's sharing setting (feature-spec §4.8).
/// </summary>
public interface IShelfShareCommandService
{
    /// <summary>
    /// Public issues a link (or keeps the current one); private deletes it, so going public
    /// again gives a new link and an old one never comes back.
    /// </summary>
    Task<ShelfSharingResponse> SetVisibilityAsync(bool isPublic, CancellationToken cancellationToken);

    /// <summary>Replaces the link. Throws <see cref="ConflictException"/> while the shelf is private.</summary>
    Task<ShelfSharingResponse> RegenerateAsync(CancellationToken cancellationToken);
}
