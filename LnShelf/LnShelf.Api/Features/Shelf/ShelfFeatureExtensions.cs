namespace LnShelf.Api.Features.Shelf;

/// <summary>
/// The Shelf slice registers itself; the composition root only calls this.
/// </summary>
public static class ShelfFeatureExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddShelf(IConfiguration configuration)
        {
            // The same validated binding every other slice uses. ShelfOptions carries only a
            // flag today, so nothing is annotated and nothing is checked — the point is that
            // the next option added here is validated without anyone remembering to ask.
            services.AddOptions<ShelfOptions>()
                .Bind(configuration.GetSection(ShelfOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddScoped<ILibraryQueryService, LibraryQueryService>();
            services.AddScoped<ITrackedSeriesQueryService, TrackedSeriesQueryService>();

            // One registration per source the shelf can add from. This is the whole of
            // what a third source costs: a new IAddSeriesFlow and a line here, with no
            // edit to SeriesCommandService or to the controller.
            services.AddScoped<IAddSeriesFlow, AniListAddSeriesFlow>();
            services.AddScoped<IAddSeriesFlow, MangaUpdatesAddSeriesFlow>();

            services.AddScoped<AddSeriesPipeline>();
            services.AddScoped<ISeriesCommandService, SeriesCommandService>();
            services.AddScoped<ISeriesDetailQueryService, SeriesDetailQueryService>();
            services.AddScoped<IVolumeCommandService, VolumeCommandService>();
            services.AddScoped<IVolumeReadDateCommandService, VolumeReadDateCommandService>();
            services.AddScoped<ShelfSeeder>();
            return services;
        }
    }
}
