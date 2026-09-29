using LnShelf.Client;
using LnShelf.Client.Features.Changelog;
using LnShelf.Client.Features.Import;
using LnShelf.Client.Features.Library;
using LnShelf.Client.Features.Search;
using LnShelf.Client.Features.Series;
using LnShelf.Client.Features.Stats;
using LnShelf.Client.Features.Sync;
using LnShelf.Client.Features.Upcoming;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// A plain relative-path client: no tokens, no auth handlers (feature-spec §7).
// In production nginx proxies /api/* onto this same origin, so the host base
// address is the right one. In development the WASM dev server and Kestrel are
// separate origins, so wwwroot/appsettings.Development.json points at the API.
var apiBaseAddress = builder.Configuration["ApiBaseAddress"] ?? builder.HostEnvironment.BaseAddress;
// The timeout matches nginx's proxy_read_timeout on /api/ (300s, nginx.conf): an import
// matches every series against AniList inside one POST, rate-limit back-off included, and
// HttpClient's default 100s gave up on it long before the proxy would have.
builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = new Uri(apiBaseAddress),
    Timeout = TimeSpan.FromSeconds(300),
});

builder.Services.AddScoped<ApiUrlBuilder>();

// Each slice owns its own registration (CLAUDE.md), exactly as the Api's features do.
builder.Services
    .AddLibrary()
    .AddSearch()
    .AddSeries()
    .AddPreferences()
    .AddImport()
    .AddSync()
    .AddChangelog()
    .AddUpcoming()
    .AddStats();

await builder.Build().RunAsync();
