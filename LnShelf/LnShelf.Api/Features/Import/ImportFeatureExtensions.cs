using LnShelf.Api.Commands;
using LnShelf.Api.Features.Import.Abstractions;
using LnShelf.Api.Features.Import.Commands;
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

            // Collects the ISBNs earlier imports parsed and dropped. Two callers: the
            // one-shot command, and add-series adopting them for one series so enrichment
            // has a key to work with.
            services.AddScoped<IIsbnBackfill, IsbnBackfillService>();
            services.AddScoped<IConsoleCommand, IsbnBackfillCommand>();

            return services;
        }
    }
}
