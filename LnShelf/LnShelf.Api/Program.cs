using LnShelf.Api.Features.Metadata;
using LnShelf.Api.Features.Shelf;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddLnShelfData(builder.Configuration);
builder.Services.AddShelf();
builder.Services.AddMetadata(builder.Configuration);

// Development only. In production nginx serves the client and proxies /api/*, so
// browser and API share one origin and no CORS policy exists (feature-spec §2).
// The WASM dev server is its own origin, hence this dev-only exception.
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors();
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    var clientOrigin = builder.Configuration["Development:ClientOrigin"] ?? "http://localhost:5081";
    app.UseCors(policy => policy.WithOrigins(clientOrigin).AllowAnyHeader().AllowAnyMethod());
}

// No HTTPS redirection: TLS terminates at the reverse proxy (feature-spec §2).
app.MapControllers();

await app.MigrateAndSeedAsync();
await app.RunAsync();
