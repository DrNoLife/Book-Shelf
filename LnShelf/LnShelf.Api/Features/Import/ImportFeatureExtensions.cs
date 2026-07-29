using LnShelf.Api.Features.Import.Abstractions;
using LnShelf.Api.Features.Import.Models;
using LnShelf.Api.Features.Import.Services;

namespace LnShelf.Api.Features.Import;

/// <summary>
/// The Import slice registers itself; the composition root only calls this.
/// </summary>
public static class ImportFeatureExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddImport(IConfiguration configuration)
        {
            services.Configure<ImportOptions>(configuration.GetSection(ImportOptions.SectionName));

            services.AddScoped<StoryGraphCsvReader>();
            services.AddScoped<ISeriesMatcher, AniListSeriesMatcher>();

            // StoryGraph is the first IShelfImporter. A Goodreads or MyAnimeList
            // export becomes a second registration behind the same seam.
            services.AddScoped<IShelfImporter, StoryGraphImporter>();

            services.AddScoped<ImportShelfWriter>();
            services.AddScoped<IImportQueryService, ImportQueryService>();
            services.AddScoped<IImportCommandService, ImportCommandService>();

            return services;
        }
    }
}
