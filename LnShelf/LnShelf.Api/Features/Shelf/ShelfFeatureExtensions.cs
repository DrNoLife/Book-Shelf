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
            services.Configure<ShelfOptions>(configuration.GetSection(ShelfOptions.SectionName));

            services.AddScoped<ILibraryQueryService, LibraryQueryService>();
            services.AddScoped<ISeriesCommandService, SeriesCommandService>();
            services.AddScoped<ShelfSeeder>();
            return services;
        }
    }
}
