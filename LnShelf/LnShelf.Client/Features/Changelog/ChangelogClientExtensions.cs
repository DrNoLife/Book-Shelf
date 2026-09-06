namespace LnShelf.Client.Features.Changelog;

/// <summary>
/// The changelog slice registers itself; the composition root only calls this.
/// </summary>
public static class ChangelogClientExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddChangelog()
        {
            // One fetch, no writes, and only that page needs it.
            services.AddScoped<ChangelogState>();

            return services;
        }
    }
}
