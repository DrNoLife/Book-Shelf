using LnShelf.Client;
using LnShelf.Client.Features.Changelog;
using LnShelf.Client.Features.Import;
using LnShelf.Client.Features.Library;
using LnShelf.Client.Features.Search;
using LnShelf.Client.Features.Series;
using LnShelf.Client.Features.Sync;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// A plain relative-path client: no tokens, no auth handlers (feature-spec §7).
// In production nginx proxies /api/* onto this same origin, so the host base
// address is the right one. In development the WASM dev server and Kestrel are
// separate origins, so wwwroot/appsettings.Development.json points at the API.
var apiBaseAddress = builder.Configuration["ApiBaseAddress"] ?? builder.HostEnvironment.BaseAddress;
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(apiBaseAddress) });

builder.Services.AddScoped<ApiUrlBuilder>();

// Each slice owns its own registration (CLAUDE.md), exactly as the Api's features do.
builder.Services
    .AddLibrary()
    .AddSearch()
    .AddSeries()
    .AddPreferences()
    .AddImport()
    .AddSync()
    .AddChangelog();

await builder.Build().RunAsync();
