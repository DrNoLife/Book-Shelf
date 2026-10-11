namespace LnShelf.Api.Features.Releases;

/// <summary>
/// The Releases slice registers itself; the composition root only calls this.
/// </summary>
// The Upcoming query and the per-series mute. IReleaseNotifier (feature-spec §4.5) is this
// slice's seam; its implementation, Web Push, is registered by the Notifications slice, which
// owns the devices it sends to.
public static class ReleasesFeatureExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddReleases()
        {
            services.AddScoped<IUpcomingQueryService, UpcomingQueryService>();
            services.AddScoped<ISeriesNotificationCommandService, SeriesNotificationCommandService>();
            return services;
        }
    }
}
