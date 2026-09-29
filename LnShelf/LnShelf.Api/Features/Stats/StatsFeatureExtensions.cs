namespace LnShelf.Api.Features.Stats;

/// <summary>
/// The Stats slice registers itself; the composition root only calls this.
/// </summary>
public static class StatsFeatureExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddStats()
        {
            services.AddScoped<IStatsQueryService, StatsQueryService>();
            return services;
        }
    }
}
