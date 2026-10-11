namespace LnShelf.Api.Features.Auth.Abstractions;

/// <summary>
/// The named seam for downloading the identity provider's profile picture. Over HTTP today
/// (<c>ProviderAvatarFetcher</c>); a provider that hands out pictures some other way is a new
/// implementation, not an edit.
/// </summary>
public interface IProviderAvatarFetcher
{
    /// <returns>
    /// The picture, normalised to a small webp, or null when the source sent something that is
    /// not a usable image. Throws when the download itself fails; the caller decides what a
    /// failure costs.
    /// </returns>
    Task<byte[]?> FetchAsync(Uri pictureUri, CancellationToken cancellationToken);
}
