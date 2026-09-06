namespace LnShelf.Client.Features.Sync;

/// <summary>
/// The sync slice registers itself; the composition root only calls this.
/// </summary>
public static class SyncClientExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddSync()
        {
            // The shell's sync footer, on every page.
            services.AddScoped<SyncState>();

            return services;
        }
    }
}
