using LnShelf.Shared.Calendar;

namespace LnShelf.Api.Features.Calendar.Abstractions;

/// <summary>
/// The signed-in user's own release calendar setting.
/// </summary>
public interface ICalendarFeedQueryService
{
    Task<CalendarFeedResponse> GetAsync(CancellationToken cancellationToken);
}
