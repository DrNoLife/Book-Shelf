namespace LnShelf.Client.Features.Preferences;

/// <summary>
/// The preferences slice registers itself; the composition root only calls this.
/// </summary>
public static class PreferencesClientExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddPreferences()
        {
            // One row, held once for the whole shell.
            services.AddScoped<PreferencesState>();

            return services;
        }
    }
}
