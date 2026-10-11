namespace LnShelf.Client.Features.Calendar;

/// <summary>
/// The calendar slice registers itself; the composition root only calls this.
/// </summary>
public static class CalendarClientExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddCalendar()
        {
            // One instance per app session, read by the Profile card and by Upcoming.
            services.AddScoped<CalendarFeedState>();

            return services;
        }
    }
}
