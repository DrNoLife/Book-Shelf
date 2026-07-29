using LnShelf.Client;
using LnShelf.Client.Features.Library;
using LnShelf.Client.Features.Search;
using Microsoft.AspNetCore.Components.Web;
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

// The grid and the sidebar render from one library fetch.
builder.Services.AddScoped<LibraryState>();
builder.Services.AddScoped<SeriesSearchState>();

await builder.Build().RunAsync();
