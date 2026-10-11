namespace LnShelf.Api.Features.Auth.Abstractions;

/// <summary>The signed-in user's own avatar, and nobody else's.</summary>
public interface IAvatarQueryService
{
    /// <returns>
    /// The uploaded picture when there is one, else the provider picture's copy; null when the
    /// user has neither, and the client draws the initial.
    /// </returns>
    Task<AvatarImage?> GetCurrentAsync(CancellationToken cancellationToken);
}
