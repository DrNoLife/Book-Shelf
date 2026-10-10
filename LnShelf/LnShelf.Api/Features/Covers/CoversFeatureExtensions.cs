using LnShelf.Api.Commands;
using LnShelf.Api.Features.Covers.Commands;
using Minio;

namespace LnShelf.Api.Features.Covers;

/// <summary>
/// The Covers slice registers itself; the composition root only calls this.
/// </summary>
public static class CoversFeatureExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddCovers(IConfiguration configuration)
        {
            // Validated at startup rather than bound and hoped for: a bad cover size or a
            // missing folder otherwise surfaces the first time somebody adds a series,
            // which can be hours after the deployment that caused it.
            services.AddOptions<CoverImageOptions>()
                .Bind(configuration.GetSection(CoverImageOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddOptions<FileSystemCoverStoreOptions>()
                .Bind(configuration.GetSection(FileSystemCoverStoreOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            // Singleton: the store holds nothing but the resolved folder.
            services.AddSingleton<CoverStoreRoot>();
            services.AddSingleton<ICoverStore, FileSystemCoverStore>();
            services.AddHostedService<CoverStoreStartupProbe>();

            services.AddMinioMigrationSource(configuration);

            // Stateless; the options it reads are the only thing it holds.
            services.AddSingleton<CoverNormalizer>();

            // A plain client on its own: cover bytes come from a CDN, not from any of
            // the metadata APIs, so they must not spend the query budgets the
            // rate-limit handlers protect.
            services.AddHttpClient<CoverFetcher>((provider, httpClient) =>
            {
                var covers = provider.GetRequiredService<IOptions<CoverImageOptions>>().Value;
                httpClient.Timeout = covers.FetchTimeout;
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(LnShelfUserAgent.Value);
            });

            // All three caches are key-builders over the one fetcher: a series cover keyed
            // on its source's id, the AniList-shaped adapter onto that for the callers who
            // hold a media id and nothing else, and a volume's synced art keyed on the
            // volume.
            services.AddScoped<ISeriesCoverCache, SeriesCoverCache>();
            services.AddScoped<ICoverCache, AniListCoverCache>();
            services.AddScoped<IVolumeCoverCache, VolumeCoverCache>();

            // The one-shot that deletes uploads whose volume is gone from every shelf.
            services.AddScoped<CoverOrphanSweeper>();
            services.AddScoped<IConsoleCommand, CoverOrphanSweepCommand>();

            return services;
        }

        /// <summary>
        /// The retired MinIO store as cover-migrate's source, only where one is configured.
        /// </summary>
        // Keyed, so an ICoverStore asked for anywhere else is always the folder. Registered
        // only when Covers:Minio:Endpoint is set, which keeps a machine without MinIO (dev,
        // tests, and production once migrated) from needing its settings to start, and keeps
        // the console runner, which resolves every command, from resolving a store that is not
        // there.
        private void AddMinioMigrationSource(IConfiguration configuration)
        {
            if (string.IsNullOrWhiteSpace(configuration[$"{MinioCoverStoreOptions.SectionName}:Endpoint"]))
            {
                return;
            }

            services.AddOptions<MinioCoverStoreOptions>()
                .Bind(configuration.GetSection(MinioCoverStoreOptions.SectionName))
                .ValidateDataAnnotations();

            services.AddSingleton<IMinioClient>(provider =>
            {
                var minio = provider.GetRequiredService<IOptions<MinioCoverStoreOptions>>().Value;

                return new MinioClient()
                    .WithEndpoint(minio.Endpoint)
                    .WithCredentials(minio.AccessKey, minio.SecretKey)
                    .WithSSL(minio.UseSsl)
                    .Build();
            });

            services.AddKeyedSingleton<ICoverStore, MinioCoverStore>(MinioServiceKey);
            services.AddSingleton<CoverMigrator>();
            services.AddScoped<IConsoleCommand, CoverMigrateCommand>();
        }
    }

    /// <summary>The service key the retired MinIO store is registered under.</summary>
    public const string MinioServiceKey = "minio";
}
