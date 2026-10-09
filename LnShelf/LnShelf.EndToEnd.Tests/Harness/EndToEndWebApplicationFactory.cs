using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LnShelf.EndToEnd.Tests.Harness;

/// <summary>
/// The real API composition root on a real Kestrel port, with every external source
/// replaced at its seam and the client bundle served from the same origin.
/// </summary>
public sealed class EndToEndWebApplicationFactory : WebApplicationFactory<Program>
{
    // Nothing listens on the discard port, so a source the fakes missed fails at once
    // instead of reaching AniList, MangaUpdates, Open Library or Apple Books.
    private const string UnreachableEndpoint = "http://127.0.0.1:9/";

    private readonly string _connectionString;
    private readonly string _bundlePath;

    public EndToEndWebApplicationFactory(string connectionString, string bundlePath)
    {
        _connectionString = connectionString;
        _bundlePath = bundlePath;
        UseKestrel(0);
    }

    public InMemoryCoverStore CoverStore { get; } = new();

    public Uri BaseAddress
    {
        get
        {
            var addresses = Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()
                ?? throw new InvalidOperationException("The host exposes no server addresses.");
            return new Uri(addresses.Addresses.First());
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("EndToEnd");

        // UseSetting rather than ConfigureAppConfiguration: AddLnShelfData reads the
        // connection string while services are registered, before app configuration
        // added by the factory would be visible.
        builder.UseSetting("ConnectionStrings:Db", _connectionString);
        builder.UseSetting("Shelf:SeedDevelopmentFixture", "true");
        builder.UseSetting("Sync:Enabled", "false");
        builder.UseSetting("AniList:Endpoint", UnreachableEndpoint);
        builder.UseSetting("MangaUpdates:Endpoint", UnreachableEndpoint);
        builder.UseSetting("OpenLibrary:Endpoint", UnreachableEndpoint);
        builder.UseSetting("OpenLibrary:CoverEndpoint", UnreachableEndpoint);
        builder.UseSetting("AppleBooks:Endpoint", UnreachableEndpoint);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ISeriesMetadataSource>();
            services.AddScoped<ISeriesMetadataSource, FixtureSeriesMetadataSource>();
            services.RemoveAll<IMangaUpdatesSeriesQueryService>();
            services.AddScoped<IMangaUpdatesSeriesQueryService, SilentMangaUpdatesSeriesQueryService>();
            services.RemoveAll<IReleaseSource>();
            services.AddScoped<IReleaseSource, SilentReleaseSource>();
            services.RemoveAll<IVolumeCatalogueSource>();
            services.AddScoped<IVolumeCatalogueSource, SilentVolumeCatalogueSource>();
            services.RemoveAll<IVolumeEnrichmentSource>();
            services.AddScoped<IVolumeEnrichmentSource, SilentVolumeEnrichmentSource>();
            services.RemoveAll<IIsbnCandidateSource>();
            services.AddScoped<IIsbnCandidateSource, SilentIsbnCandidateSource>();

            services.RemoveAll<ICoverStore>();
            services.AddSingleton<ICoverStore>(CoverStore);
            services.AddHttpClient<CoverFetcher>().ConfigurePrimaryHttpMessageHandler(() => new StubImageHandler());

            services.AddSingleton<IStartupFilter>(new ClientBundleStartupFilter(_bundlePath));
        });
    }
}
