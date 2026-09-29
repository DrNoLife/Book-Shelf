namespace LnShelf.Client.Features.Stats;

/// <summary>
/// The Stats slice registers itself; the composition root only calls this.
/// </summary>
public static class StatsClientExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddStats()
        {
            services.AddScoped<StatsState>();

            return services;
        }
    }
}
