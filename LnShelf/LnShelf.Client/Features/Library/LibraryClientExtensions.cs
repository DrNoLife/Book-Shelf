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

            // The status and format filters, remembered per browser.
            services.AddScoped<LibraryFilterMemory>();

            // "I finished Vol N" and its undo, shared by the desktop card and the phone carousel.
            services.AddScoped<FinishVolumeState>();

            return services;
        }
    }
}
