using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;

namespace LnShelf.EndToEnd.Tests.Harness;

/// <summary>
/// Plays nginx for the test host: serves the published client bundle and falls back to
/// index.html for client routes, so the browser and <c>/api</c> share one origin exactly
/// as in production (nginx.conf). This is also where a proxy-injected auth header goes
/// once feature-spec §7's internal header exists.
/// </summary>
public sealed class ClientBundleStartupFilter(string bundlePath) : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        // nginx's `try_files $uri $uri/ /index.html`: anything that is not the API and does
        // not name a file is a client route, so the bundle's router decides what it shows.
        app.Use((context, nextMiddleware) =>
        {
            var path = context.Request.Path;
            if (HttpMethods.IsGet(context.Request.Method)
                && !path.StartsWithSegments("/api")
                && !Path.HasExtension(path.Value))
            {
                context.Request.Path = "/index.html";
            }

            return nextMiddleware(context);
        });

        // Unknown types served as octet-stream, like nginx's `default_type`, so the
        // fingerprinted _framework files are never refused for their extension.
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(bundlePath),
            ServeUnknownFileTypes = true,
            DefaultContentType = "application/octet-stream",
        });

        next(app);
    };
}
