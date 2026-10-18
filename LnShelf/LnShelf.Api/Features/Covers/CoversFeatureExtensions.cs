using LnShelf.Api.Commands;
using LnShelf.Api.Features.Covers.Commands;

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

            // Both caches are key-builders over the one fetcher: a series cover keyed on its
            // source's id, and a volume's synced art keyed on the volume.
            services.AddScoped<ISeriesCoverCache, SeriesCoverCache>();
            services.AddScoped<IVolumeCoverCache, VolumeCoverCache>();

            // The one-shot that deletes uploads whose volume is gone from every shelf, and stray avatars.
            services.AddScoped<CoverOrphanSweeper>();
            services.AddScoped<IConsoleCommand, CoverOrphanSweepCommand>();

            // Both halves of cover sharing: reads by match key, and writes scoped by hand to the
            // current user. The shelf's cover writes stage their changes through the command side.
            services.AddScoped<ICommunityCoverQueryService, CommunityCoverQueryService>();
            services.AddScoped<ICommunityCoverCommandService, CommunityCoverCommandService>();
            services.AddScoped<IConsoleCommand, CommunityCoverWithdrawCommand>();

            return services;
        }
    }
}
