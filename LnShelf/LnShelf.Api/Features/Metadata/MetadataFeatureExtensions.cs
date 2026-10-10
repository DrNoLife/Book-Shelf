using System.Net.Http.Headers;
using LnShelf.Api.Commands;
using LnShelf.Api.Features.Metadata.Commands;
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
            services.AddSeriesSearch();
            services.AddVolumeCatalogue(configuration);
            services.AddVolumeEnrichment(configuration);
            services.AddEnrichmentVerification();
            services.AddSync(configuration);

            return services;
        }

        /// <summary>AniList behind <c>ISeriesMetadataSource</c>.</summary>
        private IServiceCollection AddAniList(IConfiguration configuration)
        {
            // Validated at startup, like every options type in this slice: these numbers size
            // request budgets and timeouts, and a bad one is otherwise found by a request that
            // behaves oddly rather than by a boot that refuses.
            services.AddOptions<AniListOptions>()
                .Bind(configuration.GetSection(AniListOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddHttpClient<AniListGraphQlClient>((provider, httpClient) =>
                {
                    var aniList = provider.GetRequiredService<IOptions<AniListOptions>>().Value;
                    httpClient.BaseAddress = aniList.Endpoint;

                    // Infinite here, and enforced per attempt inside the handler instead.
                    // HttpClient.Timeout covers the whole handler chain, 429 back-off
                    // included, so a 20-second timeout around a 60-second back-off meant the
                    // retry could never happen. See PoliteRateLimitHandler.
                    httpClient.Timeout = Timeout.InfiniteTimeSpan;
                    httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(LnShelfUserAgent.Value);
                })
                .AddSourcePoliteness(MetadataRateLimiting.AniList);

            services.AddScoped<ISeriesMetadataSource, AniListSeriesQueryService>();

            return services;
        }

        /// <summary>
        /// The search the add flow uses: AniList, with MangaUpdates behind it.
        /// </summary>
        // Registered apart from either source, because it belongs to neither. Both the
        // search endpoint and `enrichment-verify` resolve ISeriesSearchService, so there is
        // exactly one answer in the app to "what happens when AniList will not serve".
        private IServiceCollection AddSeriesSearch()
        {
            services.AddScoped<IMangaUpdatesSeriesQueryService, MangaUpdatesSeriesQueryService>();
            services.AddScoped<ISeriesSearchService, FallbackSeriesSearchService>();
            services.AddScoped<SearchHitResolver>();
            services.AddScoped<SeriesSearchPresentationService>();

            return services;
        }

        /// <summary>MangaUpdates behind <c>IReleaseSource</c> (feature-spec §4.2).</summary>
        private IServiceCollection AddReleaseSource(IConfiguration configuration)
        {
            services.AddOptions<MangaUpdatesOptions>()
                .Bind(configuration.GetSection(MangaUpdatesOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddHttpClient<MangaUpdatesClient>((provider, httpClient) =>
                {
                    var mangaUpdates = provider.GetRequiredService<IOptions<MangaUpdatesOptions>>().Value;
                    httpClient.BaseAddress = mangaUpdates.Endpoint;

                    // Infinite for the same reason as AniList's: the timeout lives on the
                    // handler, per attempt, so it cannot swallow the 429 back-off.
                    httpClient.Timeout = Timeout.InfiniteTimeSpan;
                    httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(LnShelfUserAgent.Value);
                })
                .AddSourcePoliteness(MetadataRateLimiting.MangaUpdates);

            // One cached `series/search` for both readers of it — the link search here and
            // the add-flow search in AddSeriesSearch ask the endpoint the same question.
            services.AddScoped<MangaUpdatesSearchCache>();

            services.AddScoped<IReleaseSource, MangaUpdatesReleaseSource>();
            services.AddScoped<IReleaseSourceMatcher, ReleaseSourceMatcher>();

            return services;
        }

        /// <summary>Apple Books behind <c>IVolumeCatalogueSource</c>.</summary>
        private IServiceCollection AddVolumeCatalogue(IConfiguration configuration)
        {
            services.AddOptions<AppleBooksOptions>()
                .Bind(configuration.GetSection(AppleBooksOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddHttpClient<AppleBooksClient>((provider, httpClient) =>
                {
                    var appleBooks = provider.GetRequiredService<IOptions<AppleBooksOptions>>().Value;
                    httpClient.BaseAddress = appleBooks.Endpoint;

                    // Infinite here, and enforced per request inside the handler instead —
                    // the same arrangement as Open Library's, for the same reason:
                    // HttpClient.Timeout also covers the wait for a rate-limit permit. See
                    // PoliteRateLimitHandler.
                    httpClient.Timeout = Timeout.InfiniteTimeSpan;

                    // The endpoint answers `text/javascript` whatever is asked for, which is
                    // why AppleBooksClient deserializes by hand rather than through
                    // ReadFromJsonAsync. Asking for JSON anyway is still the honest request.
                    httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(LnShelfUserAgent.Value);
                })
                .AddSourcePoliteness(MetadataRateLimiting.AppleBooks);

            services.AddScoped<IVolumeCatalogueSource, AppleBooksVolumeCatalogueSource>();

            return services;
        }

        /// <summary>Open Library behind <c>IVolumeEnrichmentSource</c> (the enrichment verdict, §3).</summary>
        private IServiceCollection AddVolumeEnrichment(IConfiguration configuration)
        {
            services.AddOptions<OpenLibraryOptions>()
                .Bind(configuration.GetSection(OpenLibraryOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddHttpClient<OpenLibraryClient>((provider, httpClient) =>
                {
                    var openLibrary = provider.GetRequiredService<IOptions<OpenLibraryOptions>>().Value;
                    httpClient.BaseAddress = openLibrary.Endpoint;

                    // Infinite here, and enforced per request inside the handler instead.
                    // HttpClient.Timeout also covers the wait for a rate-limit permit, and
                    // at twelve requests a minute that wait is minutes — see
                    // PoliteRateLimitHandler.
                    httpClient.Timeout = Timeout.InfiniteTimeSpan;

                    httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    // Contactable, because Open Library's docs ask for it.
                    httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                        LnShelfUserAgent.WithContact(openLibrary.ContactEmail));
                })
                .AddSourcePoliteness(MetadataRateLimiting.OpenLibrary);

            services.AddScoped<IVolumeEnrichmentSource, OpenLibraryVolumeEnrichmentSource>();
            services.AddScoped<IIsbnCandidateSource, OpenLibraryIsbnCandidateSource>();

            // Where the conflict rules live. The sync run calls the service per series; the
            // writer is what both of its passes route every write through.
            services.AddScoped<VolumeEnrichmentService>();
            services.AddScoped<VolumeFactWriter>();

            // The two passes the service runs in order, and the two judgements they share:
            // which record is this series' volume N, and whether a guessed ISBN proves it.
            services.AddScoped<CatalogueTitleArbiter>();
            services.AddScoped<CatalogueVolumeMatcher>();
            services.AddScoped<IsbnVolumeEnricher>();
            services.AddScoped<IsbnCandidateVerifier>();

            // The one thing the verification run needs that DI cannot express on its own:
            // the same pipeline, with the cover cache swapped for one that measures bytes
            // instead of storing them (VerificationEnrichmentRunner). Everything else still
            // comes from the container.
            //
            // Written out rather than reflected up with ActivatorUtilities, so that adding a
            // dependency to any of these three breaks the build here instead of the command
            // at run time.
            services.AddScoped<VolumeEnrichmentFactory>(provider => coverCache =>
            {
                var factWriter = new VolumeFactWriter(
                    coverCache, provider.GetRequiredService<ILogger<VolumeFactWriter>>());

                var isbnEnricher = new IsbnVolumeEnricher(
                    provider.GetRequiredService<IVolumeEnrichmentSource>(),
                    provider.GetRequiredService<IIsbnCandidateSource>(),
                    provider.GetRequiredService<IsbnCandidateVerifier>(),
                    factWriter,
                    provider.GetRequiredService<ILogger<IsbnVolumeEnricher>>());

                return new VolumeEnrichmentService(
                    provider.GetRequiredService<IVolumeCatalogueSource>(),
                    provider.GetRequiredService<CatalogueTitleArbiter>(),
                    isbnEnricher,
                    factWriter);
            });

            // The one-shots: discovery proposes into a file, apply writes what survives
            // review. Two commands because a person sits between them.
            services.AddScoped<IsbnDiscoveryService>();
            services.AddScoped<IsbnApplyService>();
            services.AddScoped<IsbnReviewFile>();
            services.AddScoped<IConsoleCommand, IsbnDiscoverCommand>();
            services.AddScoped<IConsoleCommand, IsbnApplyCommand>();

            // A third one-shot, and the only one of the three that writes unreviewed. It can:
            // the ISBN pair has to guess an identifier, where this asks the same seam the
            // nightly pass asks, about volumes it has stopped asking about, and writes one
            // column from the answer.
            services.AddScoped<SubtitleBackfillService>();
            services.AddScoped<IConsoleCommand, SubtitleBackfillCommand>();

            return services;
        }

        /// <summary>
        /// The live check that the per-volume sources still answer at all.
        /// </summary>
        // Registered like any other command, and with nothing else pointing at it: the
        // service is only ever reached by typing `enrichment-verify`, which is the point —
        // it exists to be run deliberately, not to run itself.
        private IServiceCollection AddEnrichmentVerification()
        {
            // A plain client with no rate-limit handler, exactly like the cover fetcher's:
            // cover bytes come from CDNs rather than from the metadata APIs, so verifying
            // them must not spend the budgets those handlers protect.
            services.AddHttpClient<VerificationImageProbe>(httpClient =>
            {
                httpClient.Timeout = VerificationImageProbe.RequestTimeout;
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(LnShelfUserAgent.Value);
            });

            services.AddScoped<EnrichmentVerificationService>();
            services.AddScoped<VerificationVolumeCountResolver>();
            services.AddScoped<VerificationEnrichmentRunner>();
            services.AddScoped<IConsoleCommand, EnrichmentVerifyCommand>();

            return services;
        }

        private IServiceCollection AddSync(IConfiguration configuration)
        {
            // The one worth spelling out: Sync__Hour is set in the production compose file, and
            // a value outside 0-23 used to bind cleanly and then throw from inside the nightly
            // loop — which .NET answers by stopping the whole host. Now it refuses to boot,
            // naming the field.
            services.AddOptions<SyncOptions>()
                .Bind(configuration.GetSection(SyncOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            // What happens to one series, shared by the nightly sweep and the refresh that
            // runs when a series is added (feature-spec §4.2 — "same code path").
            services.AddScoped<SeriesRefresher>();
            services.AddScoped<ReleaseCountWriter>();
            services.AddScoped<PublicationStatusWriter>();

            services.AddScoped<ISeriesSyncService, SeriesSyncService>();
            services.AddScoped<ISyncQueryService, SyncQueryService>();

            // The user's pick of a source where the sources disagree. The applier is shared by
            // the command service; the sync writers read the choices it saves.
            services.AddScoped<SourceChoiceApplier>();
            services.AddScoped<ISourceChoiceCommandService, SourceChoiceCommandService>();
            services.AddScoped<ISourceConflictQueryService, SourceConflictQueryService>();

            // One nightly loop, no job framework (CLAUDE.md).
            services.AddHostedService<NightlySyncBackgroundService>();

            // "Sync now" runs in the background too. Registered as a singleton first and then
            // as the hosted service, so the controller and the host share the one instance
            // that owns the queue. The gate is what keeps it and the nightly loop off the same
            // shelf at once.
            services.AddSingleton<SyncRunGate>();
            services.AddSingleton<ManualSyncRunner>();
            services.AddHostedService(provider => provider.GetRequiredService<ManualSyncRunner>());

            return services;
        }
    }
}
