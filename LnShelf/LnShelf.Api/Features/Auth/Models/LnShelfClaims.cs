namespace LnShelf.Api.Features.Auth.Models;

/// <summary>The claims this app adds to a session on top of the provider's.</summary>
public static class LnShelfClaims
{
    /// <summary>The <see cref="User"/> row's id. The session is useless without it.</summary>
    public const string UserId = "lnshelf:user";
}
