namespace LnShelf.Shared.Auth;

/// <summary>
/// Who is signed in, as the profile page shows them.
/// </summary>
// There is no name or avatar from the provider: the sign-in asks Google for the email scope
// alone (the privacy page says so), so the display name is derived from the address.
public sealed record CurrentUserResponse
{
    public required string DisplayName { get; init; }

    public required string Email { get; init; }

    /// <summary>When the account was created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }
}
