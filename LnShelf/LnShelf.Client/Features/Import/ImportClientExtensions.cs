namespace LnShelf.Client.Features.Import;

/// <summary>
/// The import slice registers itself; the composition root only calls this.
/// </summary>
public static class ImportClientExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddImport()
        {
            services.AddScoped<ImportState>();

            return services;
        }
    }
}
