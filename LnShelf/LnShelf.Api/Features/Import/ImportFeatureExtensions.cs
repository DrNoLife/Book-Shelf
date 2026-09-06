using LnShelf.Api.Commands;
using LnShelf.Api.Features.Import.Commands;

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
            // Validated on start, not on first use: every one of these is a
            // threshold, and a mistyped environment variable would otherwise show up
            // as a matcher that accepts everything or offers nothing — working
            // software with the wrong opinions.
            services.AddOptions<ImportOptions>()
                .Bind(configuration.GetSection(ImportOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddScoped<StoryGraphCsvReader>();
            services.AddScoped<GoodreadsCsvReader>();
            services.AddScoped<ISeriesMatcher, AniListSeriesMatcher>();
            services.AddScoped<ImportBatchBuilder>();

            // Both registered under the seam, neither of them "the" importer: the
            // resolver asks each one whether it recognises the uploaded file. A
            // MyAnimeList export is one more line here and nothing else.
            services.AddScoped<IShelfImporter, StoryGraphImporter>();
            services.AddScoped<IShelfImporter, GoodreadsImporter>();
            services.AddScoped<ImportSourceResolver>();

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
