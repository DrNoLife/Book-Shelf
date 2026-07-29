namespace LnShelf.Api.Features.Shelf;

/// <summary>
/// The Shelf slice registers itself; the composition root only calls this.
/// </summary>
public static class ShelfFeatureExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddShelf()
        {
            services.AddScoped<ILibraryQueryService, LibraryQueryService>();
            services.AddScoped<ShelfSeeder>();
            return services;
        }
    }
}
