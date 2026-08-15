using System.Net.Http.Headers;
using LnShelf.Api.Commands;
using LnShelf.Api.Features.Metadata.Commands;
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
            services.AddVolumeCatalogue(configuration);
            services.AddVolumeEnrichment(configuration);
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

        /// <summary>Apple Books behind <c>IVolumeCatalogueSource</c>.</summary>
        private IServiceCollection AddVolumeCatalogue(IConfiguration configuration)
        {
            services.Configure<AppleBooksOptions>(configuration.GetSection(AppleBooksOptions.SectionName));

            // Its own pool: the four APIs have unrelated budgets.
            services.AddSingleton<AppleBooksRateLimiter>();
            services.AddTransient<AppleBooksRateLimitHandler>();

            services.AddHttpClient<AppleBooksClient>((provider, httpClient) =>
                {
                    var appleBooks = provider.GetRequiredService<IOptions<AppleBooksOptions>>().Value;
                    httpClient.BaseAddress = appleBooks.Endpoint;

                    // Infinite here, and enforced per request inside the handler instead —
                    // the same arrangement as Open Library's, for the same reason:
                    // HttpClient.Timeout also covers the wait for a rate-limit permit. See
                    // AppleBooksRateLimitHandler.
                    httpClient.Timeout = Timeout.InfiniteTimeSpan;

                    // The endpoint answers `text/javascript` whatever is asked for, which is
                    // why AppleBooksClient deserializes by hand rather than through
                    // ReadFromJsonAsync. Asking for JSON anyway is still the honest request.
                    httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(LnShelfUserAgent.Value);
                })
                .AddHttpMessageHandler<AppleBooksRateLimitHandler>();

            services.AddScoped<IVolumeCatalogueSource, AppleBooksVolumeCatalogueSource>();

            return services;
        }

        /// <summary>Open Library behind <c>IVolumeEnrichmentSource</c> (the enrichment verdict, §3).</summary>
        private IServiceCollection AddVolumeEnrichment(IConfiguration configuration)
        {
            services.Configure<OpenLibraryOptions>(configuration.GetSection(OpenLibraryOptions.SectionName));

            // Its own pool, and the smallest: Open Library is a donated catalogue with
            // a documented ceiling, unrelated to the other two APIs' budgets.
            services.AddSingleton<OpenLibraryRateLimiter>();
            services.AddTransient<OpenLibraryRateLimitHandler>();

            services.AddHttpClient<OpenLibraryClient>((provider, httpClient) =>
                {
                    var openLibrary = provider.GetRequiredService<IOptions<OpenLibraryOptions>>().Value;
                    httpClient.BaseAddress = openLibrary.Endpoint;

                    // Infinite here, and enforced per request inside the handler instead.
                    // HttpClient.Timeout also covers the wait for a rate-limit permit, and
                    // at twelve requests a minute that wait is minutes — see
                    // OpenLibraryRateLimitHandler.
                    httpClient.Timeout = Timeout.InfiniteTimeSpan;

                    httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    // Contactable, because Open Library's docs ask for it.
                    httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                        LnShelfUserAgent.WithContact(openLibrary.ContactEmail));
                })
                .AddHttpMessageHandler<OpenLibraryRateLimitHandler>();

            services.AddScoped<IVolumeEnrichmentSource, OpenLibraryVolumeEnrichmentSource>();
            services.AddScoped<IIsbnCandidateSource, OpenLibraryIsbnCandidateSource>();

            // Where the conflict rules live. The sync run calls the service per series; the
            // writer is what both of its passes route every write through.
            services.AddScoped<VolumeEnrichmentService>();
            services.AddScoped<VolumeFactWriter>();

            // The one-shots: discovery proposes into a file, apply writes what survives
            // review. Two commands because a person sits between them.
            services.AddScoped<IsbnDiscoveryService>();
            services.AddScoped<IsbnApplyService>();
            services.AddScoped<IsbnReviewFile>();
            services.AddScoped<IConsoleCommand, IsbnDiscoverCommand>();
            services.AddScoped<IConsoleCommand, IsbnApplyCommand>();

            return services;
        }

        private IServiceCollection AddSync(IConfiguration configuration)
        {
            services.Configure<SyncOptions>(configuration.GetSection(SyncOptions.SectionName));

            // What happens to one series, shared by the nightly sweep and the refresh that
            // runs when a series is added (feature-spec §4.2 — "same code path").
            services.AddScoped<SeriesRefresher>();

            services.AddScoped<ISeriesSyncService, SeriesSyncService>();
            services.AddScoped<ISyncQueryService, SyncQueryService>();

            // One nightly loop, no job framework (CLAUDE.md).
            services.AddHostedService<NightlySyncBackgroundService>();

            return services;
        }
    }
}
