namespace LnShelf.Api.Features.Releases;

/// <summary>
/// The Releases slice registers itself; the composition root only calls this.
/// </summary>
// Only the Upcoming query for now. IReleaseNotifier (feature-spec §4.5) is this slice's
// other seam and arrives with its first notifier, not ahead of it.
public static class ReleasesFeatureExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddReleases()
        {
            services.AddScoped<IUpcomingQueryService, UpcomingQueryService>();
            return services;
        }
    }
}
