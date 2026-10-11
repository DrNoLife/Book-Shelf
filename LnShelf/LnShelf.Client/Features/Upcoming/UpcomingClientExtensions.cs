namespace LnShelf.Client.Features.Upcoming;

/// <summary>
/// The Upcoming slice registers itself; the composition root only calls this.
/// </summary>
public static class UpcomingClientExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddUpcoming()
        {
            services.AddScoped<UpcomingState>();
            services.AddScoped<BuyVolumeState>();

            return services;
        }
    }
}
