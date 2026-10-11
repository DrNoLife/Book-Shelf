namespace LnShelf.Api.Features.Auth.Abstractions;

/// <summary>
/// Changes to a user's avatar: the provider picture's local copy, kept fresh at sign-in, and
/// the picture the user uploads over it.
/// </summary>
public interface IAvatarCommandService
{
    /// <summary>
    /// Keeps a local copy of the provider's picture for a user who just signed in. Best effort:
    /// a failure is logged and never thrown, so a picture can never stop a sign-in.
    /// </summary>
    /// <param name="user">The signed-in user's tracked row.</param>
    /// <param name="pictureUrl">The provider's <c>picture</c> claim, or null when it sent none.</param>
    Task RefreshProviderCopyAsync(User user, string? pictureUrl, CancellationToken cancellationToken);

    /// <summary>Stores an uploaded picture as the current user's avatar.</summary>
    /// <returns>False when the current user's row is gone.</returns>
    Task<bool> SetOverrideAsync(ReadOnlyMemory<byte> content, CancellationToken cancellationToken);

    /// <summary>Removes the uploaded picture, so the provider's copy shows again.</summary>
    /// <returns>False when the current user's row is gone.</returns>
    Task<bool> RemoveOverrideAsync(CancellationToken cancellationToken);
}
