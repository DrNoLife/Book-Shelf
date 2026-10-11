namespace LnShelf.Api.Features.Auth.Models;

/// <summary>
/// One person who has signed in. Everything they track hangs off this row by OwnerId, and
/// deleting it deletes all of it (feature-spec §7).
/// </summary>
// Keyed by the provider's issuer and subject, never by email: an address can change or be
// reused, the subject cannot. The email is kept to show the account, and to let the
// configured initial owner adopt the library that existed before users did.
public sealed class User
{
    /// <summary>
    /// The row the migration to users created for the library that existed before. The
    /// address in Auth:InitialOwnerEmail adopts it, in place, on its first sign-in.
    /// </summary>
    public static readonly Guid InitialOwnerPlaceholderId = new("00000000-0000-0000-0000-00000000a001");

    /// <summary>Who the development bypass signs every request in as.</summary>
    public static readonly Guid DevelopmentUserId = new("00000000-0000-0000-0000-00000000de01");

    /// <summary>The issuer of the two rows above, which no provider ever issues.</summary>
    public const string LocalIssuer = "lnshelf";

    /// <summary>The longest provider picture URL kept; a longer one is not copied.</summary>
    // 2048 is the practical ceiling browsers and CDNs hold a URL to; Google's picture URLs are
    // a few hundred characters.
    public const int MaximumPictureUrlLength = 2048;

    public Guid Id { get; set; }

    public string Issuer { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset LastSeenAt { get; set; }

    /// <summary>
    /// The provider's picture URL at the last sign-in that carried one; what the local copy
    /// was fetched from. Compared at each sign-in so an unchanged picture is not fetched again.
    /// </summary>
    public string? ProviderPictureUrl { get; set; }

    /// <summary>The local copy of the provider's picture, in the cover store.</summary>
    // A copy rather than the URL, for the reason covers are cached: the client never loads
    // anything from a third party, and the avatar must show without Google being reachable.
    public string? ProviderAvatarObjectKey { get; set; }

    /// <summary>A picture the user uploaded. Wins over the provider's copy while it exists.</summary>
    public string? AvatarOverrideObjectKey { get; set; }
}
