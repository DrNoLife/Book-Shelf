using System.Threading.RateLimiting;
using LnShelf.Api.Features.Calendar.Abstractions;
using LnShelf.Api.Features.Calendar.Services;

namespace LnShelf.Api.Features.Calendar;

/// <summary>
/// The Calendar slice registers itself; the composition root only calls this.
/// </summary>
public static class CalendarFeatureExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddCalendar(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            services.AddOptions<CalendarOptions>()
                .Bind(configuration.GetSection(CalendarOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddScoped<ICalendarFeedResolver, CalendarFeedResolver>();
            services.AddScoped<ICalendarFeedQueryService, CalendarFeedQueryService>();
            services.AddScoped<ICalendarFeedCommandService, CalendarFeedCommandService>();
            services.AddScoped<IReleaseCalendarQueryService, ReleaseCalendarQueryService>();

            var options = configuration.GetSection(CalendarOptions.SectionName).Get<CalendarOptions>() ?? new CalendarOptions();

            // Composes with the other slices' policies, as SharingFeatureExtensions explains.
            services.AddRateLimiter(limiter =>
                limiter.AddPolicy(CalendarRateLimits.Feed, context => RateLimitPartition.GetFixedWindowLimiter(
                    // Per client address, for SharingFeatureExtensions' reason: nobody is
                    // signed in, and one token can be polled by several calendar servers.
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = options.RequestsPerMinute,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    })));

            return services;
        }
    }
}
