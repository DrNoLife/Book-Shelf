using LnShelf.Api.Features.Covers.Models;
using LnShelf.Api.Features.Covers.Services;
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
            services.Configure<CoverStoreOptions>(configuration.GetSection(CoverStoreOptions.SectionName));

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

            // Both caches are key-builders over the one fetcher: the series cover keyed
            // on an AniList media id, a volume's synced art keyed on the volume.
            services.AddScoped<ICoverCache, AniListCoverCache>();
            services.AddScoped<IVolumeCoverCache, VolumeCoverCache>();

            return services;
        }
    }
}
