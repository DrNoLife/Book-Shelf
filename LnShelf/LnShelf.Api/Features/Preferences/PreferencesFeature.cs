using LnShelf.Api.Features.Preferences.Services;

namespace LnShelf.Api.Features.Preferences;

/// <summary>
/// Reads and writes the single preferences row.
/// </summary>
// Not split into query and command services: it is one row with one getter and one
// partial setter, and two classes for that would be ceremony rather than the
// separation CLAUDE.md is asking for.
public interface IPreferencesService
{
    Task<PreferencesResponse> GetAsync(CancellationToken cancellationToken);

    Task<PreferencesResponse> UpdateAsync(
        UpdatePreferencesRequest request,
        CancellationToken cancellationToken);
}

/// <summary>The Preferences slice registers itself.</summary>
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
