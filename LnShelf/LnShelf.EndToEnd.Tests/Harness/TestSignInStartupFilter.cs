using LnShelf.Api.Features.Auth.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace LnShelf.EndToEnd.Tests.Harness;

/// <summary>
/// Signs the browser in as test user A or B (`?user=b`) without the provider: issues the real session cookie
/// through the real cookie scheme, so everything after the login is exercised as in
/// production.
/// </summary>
// Exists only in the test host, registered by EndToEndWebApplicationFactory. Production has
// no way to issue a session other than the OpenID Connect callback.
public sealed class TestSignInStartupFilter : IStartupFilter
{
    public const string SignInPath = "/test-auth/sign-in";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        // A path check rather than app.Map: Map moves the path into PathBase, and the cookie
        // handler scopes the cookie to PathBase, so the session only reached /test-auth.
        app.Use(async (context, nextMiddleware) =>
        {
            if (context.Request.Path != SignInPath)
            {
                await nextMiddleware(context);
                return;
            }

            // Through the real UserSignIn, so the user row and the session's claims are the ones
            // a provider callback would have produced.
            var user = TestUser.FromKey(context.Request.Query["user"]) ?? TestUser.A;
            var principal = await context.RequestServices.GetRequiredService<UserSignIn>()
                .SignInAsync(user.ProviderIdentity(), context.RequestAborted)
                ?? throw new InvalidOperationException($"UserSignIn refused {user.Email}.");

            await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
            context.Response.StatusCode = StatusCodes.Status204NoContent;
        });

        next(app);
    };
}
