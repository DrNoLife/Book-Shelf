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

    public Guid Id { get; set; }

    public string Issuer { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset LastSeenAt { get; set; }
}
