using System.Security.Claims;

namespace LnShelf.Api.Features.Auth.Services;

/// <summary>
/// Whether a signed-in identity is the owner. Checked before a session cookie is issued,
/// so nobody else ever holds one.
/// </summary>
public static class OwnerAllowlist
{
    public static bool Admits(ClaimsPrincipal principal, string allowedEmail)
    {
        ArgumentNullException.ThrowIfNull(principal);

        if (string.IsNullOrWhiteSpace(allowedEmail))
        {
            return false;
        }

        var email = principal.FindFirst("email")?.Value ?? principal.FindFirst(ClaimTypes.Email)?.Value;

        // Verified too, not only equal: a provider can return an address the account holder
        // never proved they own.
        var verified = string.Equals(principal.FindFirst("email_verified")?.Value, "true", StringComparison.OrdinalIgnoreCase);

        return verified && string.Equals(email, allowedEmail.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
