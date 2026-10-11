namespace LnShelf.Shared.Activity;

/// <summary>"Got it" on "Since your last visit".</summary>
public sealed record MarkActivitySeenRequest
{
    /// <summary>
    /// The <see cref="ActivityResponse.Through"/> of the panel the reader dismissed: news up to
    /// it counts as seen, anything newer stays. Null means everything until now.
    /// </summary>
    public DateTimeOffset? Through { get; init; }
}
