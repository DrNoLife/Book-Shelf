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

            // A plain client on its own: cover bytes come from AniList's CDN, not
            // its GraphQL API, so they must not spend the 90-per-minute query
            // budget the rate-limit handler protects.
            services.AddHttpClient<ICoverCache, AniListCoverCache>((provider, httpClient) =>
            {
                var covers = provider.GetRequiredService<IOptions<CoverStoreOptions>>().Value;
                httpClient.Timeout = covers.FetchTimeout;
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(LnShelfUserAgent.Value);
            });

            return services;
        }
    }
}
