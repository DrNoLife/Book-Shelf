using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace LnShelf.Api.Features.Auth.Controllers;

/// <summary>
/// Sends the browser to the provider to sign in, and ends the session.
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IOptions<AuthOptions> options,
    AccountDeletionService accountDeletion,
    ICurrentUserQueryService currentUserQuery,
    IAccountExportQueryService accountExportQuery) : ControllerBase
{
    // Indented because a person may open the file, and the web defaults so it reads like every
    // other response this API sends.
    private static readonly JsonSerializerOptions ExportJsonOptions =
        new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <summary>Who is signed in: the profile page's header and account card.</summary>
    [HttpGet("me")]
    public async Task<ActionResult<CurrentUserResponse>> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        var user = await currentUserQuery.GetAsync(cancellationToken);

        return user is null ? NotFound() : Ok(user);
    }

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
    /// A JSON file of everything stored about the signed-in user (GDPR Art. 15 and 20).
    /// </summary>
    // A plain GET that answers with an attachment, so the profile page's "Export my data" is a
    // link the browser follows with its session cookie: no script, which the CSP would not
    // mind either way, and nothing for a client to buffer.
    [HttpGet("account/export")]
    public async Task<IActionResult> ExportAccountAsync(CancellationToken cancellationToken)
    {
        var export = await accountExportQuery.ExportAsync(cancellationToken);

        if (export is null)
        {
            return NotFound();
        }

        // Personal data, so nothing along the way may keep a copy.
        Response.Headers.CacheControl = "no-store";

        var fileName = $"lnshelf-export-{export.ExportedAt:yyyy-MM-dd}.json";
        var content = JsonSerializer.SerializeToUtf8Bytes(export, ExportJsonOptions);

        return File(content, "application/json", fileName);
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
