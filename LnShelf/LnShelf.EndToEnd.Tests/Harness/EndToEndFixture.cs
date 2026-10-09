using LnShelf.Api.Features.Shelf.Data;
using Microsoft.Extensions.DependencyInjection;

namespace LnShelf.EndToEnd.Tests.Harness;

/// <summary>
/// One Postgres, one host and one browser for the whole run; every journey gets a fresh
/// shelf and a fresh browser context from it.
/// </summary>
public sealed class EndToEndFixture : IAsyncLifetime
{
    private const string BundlePathVariable = "LNSHELF_CLIENT_BUNDLE";
    private const string HeadedVariable = "LNSHELF_HEADED";

    private readonly PostgresDatabase _database = new();
    private EndToEndWebApplicationFactory? _factory;
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public static string TracesDirectory => Path.Combine(AppContext.BaseDirectory, "playwright-traces");

    private EndToEndWebApplicationFactory Factory =>
        _factory ?? throw new InvalidOperationException("The fixture has not been initialised.");

    public InMemoryCoverStore CoverStore => Factory.CoverStore;

    public async Task InitializeAsync()
    {
        await _database.StartAsync();

        _factory = new EndToEndWebApplicationFactory(_database.ConnectionString, ResolveBundlePath());
        _factory.StartServer();

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = Environment.GetEnvironmentVariable(HeadedVariable) != "true",
        });
    }

    /// <summary>Back to the seeded shelf with no stored covers.</summary>
    // Through the host's own seeder, so the journeys start from the same 24 series a
    // development database starts from.
    public async Task ResetAsync()
    {
        await _database.EmptyAsync();

        await using var scope = Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ShelfSeeder>().SeedAsync(CancellationToken.None);

        CoverStore.Clear();
    }

    /// <summary>
    /// The only place a browser context is created, so a login or stored auth state
    /// lands here once the app has one.
    /// </summary>
    public async Task<IBrowserContext> NewContextAsync()
    {
        var browser = _browser ?? throw new InvalidOperationException("The fixture has not been initialised.");

        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = Factory.BaseAddress.ToString(),
        });

        await context.Tracing.StartAsync(new TracingStartOptions { Screenshots = true, Snapshots = true });

        return context;
    }

    public async Task DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.DisposeAsync();
        }

        _playwright?.Dispose();

        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await _database.DisposeAsync();
    }

    // The published bundle, not bin/ output: index.html's asset placeholders are only
    // rewritten on publish (OverrideHtmlAssetPlaceholders in the client project).
    private static string ResolveBundlePath()
    {
        var configured = Environment.GetEnvironmentVariable(BundlePathVariable);
        var bundlePath = configured ?? Path.Combine(
            FindSolutionDirectory(), "LnShelf.Client", "bin", "Release", "net10.0", "publish", "wwwroot");

        if (!File.Exists(Path.Combine(bundlePath, "index.html")))
        {
            throw new InvalidOperationException(
                $"No published client bundle at '{bundlePath}'. Run `dotnet publish LnShelf/LnShelf.Client -c Release` " +
                $"first, or point {BundlePathVariable} at a published wwwroot.");
        }

        return bundlePath;
    }

    private static string FindSolutionDirectory()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "LnShelf.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("LnShelf.slnx was not found above the test output directory.");
    }
}

[CollectionDefinition(Name)]
public sealed class EndToEndCollection : ICollectionFixture<EndToEndFixture>
{
    // Serial on purpose: the journeys share one database and reset it between tests.
    public const string Name = "EndToEnd";
}
