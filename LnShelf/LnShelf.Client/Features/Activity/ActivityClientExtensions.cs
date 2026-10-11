namespace LnShelf.Client.Features.Activity;

/// <summary>
/// The activity slice registers itself; the composition root only calls this.
/// </summary>
public static class ActivityClientExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddActivity()
        {
            // "Since your last visit" on the library page.
            services.AddScoped<ActivityState>();
            return services;
        }
    }
}
