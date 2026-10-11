namespace LnShelf.Api.Features.Calendar.Abstractions;

/// <summary>
/// Turns a release calendar's token into the user whose calendar it is.
/// </summary>
public interface ICalendarFeedResolver
{
    /// <returns>
    /// The owner, or null for a malformed, unknown or revoked token. The three are
    /// indistinguishable on purpose.
    /// </returns>
    Task<Guid?> FindOwnerAsync(string token, CancellationToken cancellationToken);
}
