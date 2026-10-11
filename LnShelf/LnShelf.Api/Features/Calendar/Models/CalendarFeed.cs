namespace LnShelf.Api.Features.Calendar.Models;

/// <summary>
/// A user's release calendar is subscribable while this row exists, at the URL its token makes.
/// </summary>
// Deliberately not IOwned and not owner-filtered, for ShelfShare's reason: a calendar app
// fetches the feed with no session, so the request has to find the owner from the token
// before there is anyone to filter by, and IgnoreQueryFilters is off-limits in request code.
// It names a user and holds no shelf data. Only Features/Calendar reads it.
public sealed class CalendarFeed
{
    /// <summary>The token column's width.</summary>
    public const int MaximumTokenLength = 32;

    /// <summary>The subscribing user, and the primary key: one feed per user.</summary>
    public Guid UserId { get; set; }

    /// <summary>The secret in the feed's URL. Plain, not hashed, like ShelfShare.Token. Never logged.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>When this token was issued: turning the feed on, or the last "New link".</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
