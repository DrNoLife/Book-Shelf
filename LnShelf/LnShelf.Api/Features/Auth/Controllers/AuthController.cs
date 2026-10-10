using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace LnShelf.Api.Features.Auth.Controllers;

/// <summary>
/// Sends the browser to the provider to sign in, and ends the session.
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController(IOptions<AuthOptions> options, AccountDeletionService accountDeletion) : ControllerBase
{
    /// <summary>
    /// Starts a login, then returns to <paramref name="returnUrl"/>. Already signed in (and
    /// always, with the development bypass) it goes straight there.
    /// </summary>
    [HttpGet("login")]
    [AllowAnonymous]
    public IActionResult Login([FromQuery] string? returnUrl)
    {
        // Local only, so the login cannot be used to bounce someone to another site.
        var target = Url.IsLocalUrl(returnUrl) ? returnUrl! : "/";

        if (User.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect(target);
        }

        return Challenge(new AuthenticationProperties { RedirectUri = target }, OpenIdConnectDefaults.AuthenticationScheme);
    }

    /// <summary>Ends the session here. The provider's own session is left alone.</summary>
    // Only the cookie: Google publishes no end-session endpoint, and signing the owner out of
    // their Google account everywhere is not this app's business.
    [HttpPost("logout")]
    public async Task<IActionResult> LogoutAsync()
    {
        if (!options.Value.DevelopmentBypass)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }

        return NoContent();
    }

    /// <summary>
    /// Deletes the signed-in account and all its data, then ends the session.
    /// </summary>
    [HttpDelete("account")]
    public async Task<IActionResult> DeleteAccountAsync(CancellationToken cancellationToken)
    {
        await accountDeletion.DeleteCurrentAccountAsync(cancellationToken);

        if (!options.Value.DevelopmentBypass)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }

        return NoContent();
    }
}
