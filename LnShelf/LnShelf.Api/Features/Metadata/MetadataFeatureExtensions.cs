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
            // HybridCache with no IDistributedCache registered is L1 only, and the
            // flag says so out loud rather than relying on that absence.
            services.AddHybridCache(cacheOptions =>
                cacheOptions.DefaultEntryOptions = new HybridCacheEntryOptions
                {
                    Expiration = TimeSpan.FromHours(1),
                    LocalCacheExpiration = TimeSpan.FromHours(1),
                    Flags = HybridCacheEntryFlags.DisableDistributedCache,
                });

            services.AddAniList(configuration);
            services.AddReleaseSource(configuration);
            services.AddSync(configuration);

            return services;
        }

        /// <summary>AniList behind <c>ISeriesMetadataSource</c>.</summary>
        private IServiceCollection AddAniList(IConfiguration configuration)
        {
            services.Configure<AniListOptions>(configuration.GetSection(AniListOptions.SectionName));

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
                    httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(LnShelfUserAgent.Value);
                })
                .AddHttpMessageHandler<AniListRateLimitHandler>();

            services.AddScoped<ISeriesMetadataSource, AniListSeriesQueryService>();
            services.AddScoped<SeriesSearchPresentationService>();

            return services;
        }

        /// <summary>MangaUpdates behind <c>IReleaseSource</c> (feature-spec §4.2).</summary>
        private IServiceCollection AddReleaseSource(IConfiguration configuration)
        {
            services.Configure<MangaUpdatesOptions>(configuration.GetSection(MangaUpdatesOptions.SectionName));

            // Its own pool: the two APIs have unrelated budgets.
            services.AddSingleton<MangaUpdatesRateLimiter>();
            services.AddTransient<MangaUpdatesRateLimitHandler>();

            services.AddHttpClient<MangaUpdatesClient>((provider, httpClient) =>
                {
                    var mangaUpdates = provider.GetRequiredService<IOptions<MangaUpdatesOptions>>().Value;
                    httpClient.BaseAddress = mangaUpdates.Endpoint;
                    httpClient.Timeout = mangaUpdates.RequestTimeout;
                    httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(LnShelfUserAgent.Value);
                })
                .AddHttpMessageHandler<MangaUpdatesRateLimitHandler>();

            services.AddScoped<IReleaseSource, MangaUpdatesReleaseSource>();
            services.AddScoped<IReleaseSourceMatcher, ReleaseSourceMatcher>();

            return services;
        }

        private IServiceCollection AddSync(IConfiguration configuration)
        {
            services.Configure<SyncOptions>(configuration.GetSection(SyncOptions.SectionName));

            services.AddScoped<ISeriesSyncService, SeriesSyncService>();
            services.AddScoped<ISyncQueryService, SyncQueryService>();

            // One nightly loop, no job framework (CLAUDE.md).
            services.AddHostedService<NightlySyncBackgroundService>();

            return services;
        }
    }
}
