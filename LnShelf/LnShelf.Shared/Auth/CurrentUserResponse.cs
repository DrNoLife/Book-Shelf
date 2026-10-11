namespace LnShelf.Shared.Auth;

/// <summary>
/// Who is signed in, as the profile page shows them.
/// </summary>
// The sign-in asks Google for the email and the basic profile. The name is not kept (the
// privacy page says so), so the display name is derived from the address. The picture is kept
// as a local copy, served by GET api/auth/me/avatar, never linked from Google.
public sealed record CurrentUserResponse
{
    public required string DisplayName { get; init; }

    public required string Email { get; init; }

    /// <summary>When the account was created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Changes whenever the avatar does; the client puts it in the avatar URL so a new picture
    /// shows at once. Null when there is no picture, and the client draws the initial.
    /// </summary>
    public string? AvatarVersion { get; init; }

    /// <summary>The avatar is one the user uploaded, so "Use Google picture" can undo it.</summary>
    public bool HasUploadedAvatar { get; init; }
}
