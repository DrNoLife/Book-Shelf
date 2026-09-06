using LnShelf.Api.Features.Preferences.Abstractions;
using LnShelf.Api.Features.Preferences.Services;

namespace LnShelf.Api.Features.Preferences;

/// <summary>
/// The Preferences slice registers itself; the composition root only calls this.
/// </summary>
public static class PreferencesFeatureExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddPreferences()
        {
            services.AddScoped<IPreferencesService, PreferencesService>();
            return services;
        }
    }
}
