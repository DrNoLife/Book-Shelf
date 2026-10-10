namespace LnShelf.Client.Features.Series;

/// <summary>
/// The series-detail slice registers itself; the composition root only calls this.
/// </summary>
public static class SeriesClientExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddSeries()
        {
            // Scoped is a WASM app's singleton. One fetch, then per-volume writes.
            services.AddScoped<SeriesDetailClient>();
            services.AddScoped<SeriesState>();
            services.AddScoped<SourceConflictState>();

            // Menu and select mode: page state, deliberately separate from the series.
            services.AddScoped<VolumeSelectionState>();

            return services;
        }
    }
}
