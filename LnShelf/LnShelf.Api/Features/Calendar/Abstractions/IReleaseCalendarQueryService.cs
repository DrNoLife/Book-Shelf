namespace LnShelf.Api.Features.Calendar.Abstractions;

/// <summary>
/// The release calendar a calendar app subscribes to (feature-spec §4.9).
/// </summary>
public interface IReleaseCalendarQueryService
{
    /// <returns>The iCalendar document, or null when the token opens nothing.</returns>
    Task<string?> GetAsync(string token, CancellationToken cancellationToken);
}
