using System.Security.Claims;
using System.Text.Encodings.Web;

namespace LnShelf.Api.Features.Auth.Services;

/// <summary>
/// Development only: every request is the owner. Registered instead of cookie and OpenID
/// Connect when Auth:DevelopmentBypass is on, which AuthOptionsValidator allows in
/// Development alone.
/// </summary>
// The WASM dev server is a separate origin, so a session cookie would not even reach the
// API there. A bypass keeps `dotnet run` working without a provider or secrets.
public sealed class DevelopmentBypassAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "DevelopmentBypass";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "Development owner")],
            SchemeName);

        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
