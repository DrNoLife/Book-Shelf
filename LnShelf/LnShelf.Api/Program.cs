using LnShelf.Api.Errors;
using LnShelf.Api.Features.Covers;
using LnShelf.Api.Features.Import;
using LnShelf.Api.Features.Metadata;
using LnShelf.Api.Features.Preferences;
using LnShelf.Api.Features.Shelf;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddLnShelfErrorHandling();
builder.Services.AddLnShelfData(builder.Configuration);
builder.Services.AddShelf(builder.Configuration);
builder.Services.AddMetadata(builder.Configuration);
builder.Services.AddCovers(builder.Configuration);
builder.Services.AddImport(builder.Configuration);
builder.Services.AddPreferences();

// Development only. In production nginx serves the client and proxies /api/*, so
// browser and API share one origin and no CORS policy exists (feature-spec §2).
// The WASM dev server is its own origin, hence this dev-only exception.
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors();
}

var app = builder.Build();

// Services throw and nothing catches on the way up; this turns what arrives here
// into a ProblemDetails response (CLAUDE.md — Error handling).
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    var clientOrigin = builder.Configuration["Development:ClientOrigin"] ?? "http://localhost:5081";
    app.UseCors(policy => policy.WithOrigins(clientOrigin).AllowAnyHeader().AllowAnyMethod());
}

// No HTTPS redirection: TLS terminates at the reverse proxy (feature-spec §2).
app.MapControllers();

await app.MigrateAndSeedAsync();
await app.RunAsync();
