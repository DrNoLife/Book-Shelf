namespace LnShelf.Api.Features.Sharing.Models;

/// <summary>
/// A user's shelf is public while this row exists, at the link its token makes (feature-spec §4.8).
/// </summary>
// Deliberately not IOwned and not owner-filtered: an anonymous request has to find the owner
// from the token before there is anyone to filter by, and IgnoreQueryFilters is off-limits
// in request code. It names a user the way app_user's issuer and subject do; it holds no
// shelf data. Only Features/Sharing reads it.
public sealed class ShelfShare
{
    /// <summary>The sharing user, and the primary key: one link per shelf.</summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// <see cref="ShareToken.Length"/> base64url characters. Plain, not hashed: see
    /// ShelfShareConfiguration.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>When this token was issued: going public, or the last "New link".</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
