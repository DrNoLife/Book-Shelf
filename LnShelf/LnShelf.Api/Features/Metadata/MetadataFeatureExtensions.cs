using System.Net.Http.Headers;
using LnShelf.Api.Features.Metadata.Models;
using LnShelf.Api.Features.Metadata.Services;
using Microsoft.Extensions.Caching.Hybrid;

namespace LnShelf.Api.Features.Metadata;

/// <summary>
/// The Metadata slice registers itself; the composition root only calls this.
/// </summary>
public static class MetadataFeatureExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddMetadata(IConfiguration configuration)
        {
            services.Configure<AniListOptions>(configuration.GetSection(AniListOptions.SectionName));

            // HybridCache with no IDistributedCache registered is L1 only, and the
            // flag says so out loud rather than relying on that absence.
            services.AddHybridCache(cacheOptions =>
                cacheOptions.DefaultEntryOptions = new HybridCacheEntryOptions
                {
                    Expiration = TimeSpan.FromHours(1),
                    LocalCacheExpiration = TimeSpan.FromHours(1),
                    Flags = HybridCacheEntryFlags.DisableDistributedCache,
                });

            // Singleton: the permit pool is the shared 90-per-minute budget, and
            // the handler chain is rebuilt periodically, so the limiter cannot
            // live on the handler.
            services.AddSingleton<AniListRateLimiter>();
            services.AddTransient<AniListRateLimitHandler>();

            services.AddHttpClient<AniListGraphQlClient>((provider, httpClient) =>
                {
                    var aniList = provider.GetRequiredService<IOptions<AniListOptions>>().Value;
                    httpClient.BaseAddress = aniList.Endpoint;
                    httpClient.Timeout = aniList.RequestTimeout;
                    httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                })
                .AddHttpMessageHandler<AniListRateLimitHandler>();

            services.AddScoped<ISeriesMetadataSource, AniListSeriesQueryService>();

            return services;
        }
    }
}
