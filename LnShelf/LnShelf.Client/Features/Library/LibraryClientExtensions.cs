namespace LnShelf.Client.Features.Library;

/// <summary>
/// The library slice registers itself; the composition root only calls this.
/// </summary>
public static class LibraryClientExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddLibrary()
        {
            // The named seam: the grid and the sidebar render from one library fetch.
            services.AddScoped<LibraryState>();

            return services;
        }
    }
}
