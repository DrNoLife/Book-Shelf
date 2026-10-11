namespace LnShelf.Shared.Calendar;

/// <summary>
/// The signed-in user's release calendar setting (feature-spec §4.9).
/// </summary>
// The token, not a URL, for ShelfSharingResponse's reason: the client builds the feed's
// address from its own base address, so the API never has to know its public hostname.
public sealed record CalendarFeedResponse
{
    public required bool IsOn { get; init; }

    /// <summary>The feed's token while it is on; null while it is off.</summary>
    public string? Token { get; init; }
}

/// <summary>On issues a feed link; off deletes it.</summary>
public sealed record UpdateCalendarFeedRequest
{
    public required bool IsOn { get; init; }
}
