using LnShelf.Client;
using LnShelf.Client.Features.Activity;
using LnShelf.Client.Features.Calendar;
using LnShelf.Client.Features.Changelog;
using LnShelf.Client.Features.Import;
using LnShelf.Client.Features.Library;
using LnShelf.Client.Features.Notifications;
using LnShelf.Client.Features.Profile;
using LnShelf.Client.Features.Recommendations;
using LnShelf.Client.Features.Search;
using LnShelf.Client.Features.Series;
using LnShelf.Client.Features.Sharing;
using LnShelf.Client.Features.Stats;
using LnShelf.Client.Features.Sync;
using LnShelf.Client.Features.Upcoming;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// A plain relative-path client with no tokens: the API keeps the session in a cookie
// (feature-spec §7), and SignInRedirectHandler only reacts to a 401 by sending the browser
// to sign in. In production nginx proxies /api/* onto this same origin, so the host base
// address is the right one. In development the WASM dev server and Kestrel are
// separate origins, so wwwroot/appsettings.Development.json points at the API.
var apiBaseAddress = builder.Configuration["ApiBaseAddress"] ?? builder.HostEnvironment.BaseAddress;
// The timeout matches nginx's proxy_read_timeout on /api/ (300s, nginx.conf): an import
// matches every series against AniList inside one POST, rate-limit back-off included, and
// HttpClient's default 100s gave up on it long before the proxy would have.
builder.Services.AddScoped(provider => new HttpClient(
    new SignInRedirectHandler(provider.GetRequiredService<NavigationManager>()) { InnerHandler = new HttpClientHandler() })
{
    BaseAddress = new Uri(apiBaseAddress),
    Timeout = TimeSpan.FromSeconds(300),
});

builder.Services.AddScoped<ApiUrlBuilder>();
builder.Services.AddSingleton<NavigationTrail>();

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
    .AddStats()
    .AddProfile()
    .AddSharing()
    .AddNotifications()
    .AddActivity()
    .AddCalendar()
    .AddRecommendations();

var host = builder.Build();

// Created before the first page renders, so the trail holds the page the app opened on and
// sees every navigation after it; created lazily, it would miss whatever came first.
host.Services.GetRequiredService<NavigationTrail>();

await host.RunAsync();
