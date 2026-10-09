namespace LnShelf.EndToEnd.Tests.Harness;

/// <summary>
/// Base for every journey: a reset shelf, a fresh browser context and a Playwright trace
/// per test (open one with `pwsh bin/.../playwright.ps1 show-trace &lt;file&gt;`).
/// </summary>
[Collection(EndToEndCollection.Name)]
public abstract class JourneyTest(EndToEndFixture fixture) : IAsyncLifetime
{
    private IBrowserContext? _context;
    private IPage? _page;

    protected EndToEndFixture Fixture => fixture;

    protected IPage Page => _page ?? throw new InvalidOperationException("The journey has not been initialised.");

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();

        _context = await fixture.NewContextAsync();
        _page = await _context.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        if (_context is null)
        {
            return;
        }

        // Kept for every test, failed or not: xUnit 2 does not tell a test whether it
        // passed, and a trace of a green run is cheap to throw away.
        Directory.CreateDirectory(EndToEndFixture.TracesDirectory);
        await _context.Tracing.StopAsync(new TracingStopOptions
        {
            Path = Path.Combine(EndToEndFixture.TracesDirectory, $"{GetType().Name}-{Guid.NewGuid():N}.zip"),
        });

        await _context.DisposeAsync();
    }
}
