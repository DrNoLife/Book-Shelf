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
            // missing bucket name otherwise surfaces the first time somebody adds a series,
            // which can be hours after the deployment that caused it.
            services.AddOptions<CoverStoreOptions>()
                .Bind(configuration.GetSection(CoverStoreOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddSingleton<IMinioClient>(provider =>
            {
                var covers = provider.GetRequiredService<IOptions<CoverStoreOptions>>().Value;

                return new MinioClient()
                    .WithEndpoint(covers.Endpoint)
                    .WithCredentials(covers.AccessKey, covers.SecretKey)
                    .WithSSL(covers.UseSsl)
                    .Build();
            });

            // Singleton: the store is stateless apart from remembering that the
            // bucket exists, which is exactly what should not be re-checked per
            // request.
            services.AddSingleton<ICoverStore, MinioCoverStore>();

            // Stateless; the options it reads are the only thing it holds.
            services.AddSingleton<CoverNormalizer>();

            // A plain client on its own: cover bytes come from a CDN, not from any of
            // the metadata APIs, so they must not spend the query budgets the
            // rate-limit handlers protect.
            services.AddHttpClient<CoverFetcher>((provider, httpClient) =>
            {
                var covers = provider.GetRequiredService<IOptions<CoverStoreOptions>>().Value;
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
    }
}
