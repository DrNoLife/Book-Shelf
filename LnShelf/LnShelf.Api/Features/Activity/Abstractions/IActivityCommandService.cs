namespace LnShelf.Api.Features.Activity.Abstractions;

/// <summary>
/// The reader's one write on "Since your last visit": "Got it".
/// </summary>
public interface IActivityCommandService
{
    /// <summary>
    /// Everything that happened up to <paramref name="through"/> counts as seen; until now
    /// when it is null. Never later than now, and never earlier than what was already seen.
    /// </summary>
    Task MarkSeenAsync(DateTimeOffset? through, CancellationToken cancellationToken);
}
