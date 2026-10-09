using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace LnShelf.EndToEnd.Tests.Harness;

/// <summary>
/// Signs the browser in as the owner without the provider: issues the real session cookie
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

            var owner = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim("email", EndToEndWebApplicationFactory.OwnerEmail),
                    new Claim("email_verified", "true"),
                ],
                CookieAuthenticationDefaults.AuthenticationScheme));

            await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, owner);
            context.Response.StatusCode = StatusCodes.Status204NoContent;
        });

        next(app);
    };
}
