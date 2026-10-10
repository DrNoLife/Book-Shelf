namespace LnShelf.Api.Features.Auth.Models;

/// <summary>
/// Login through an OpenID Connect provider (feature-spec §7).
/// </summary>
// ClientSecret is a credential: it arrives as Auth__ClientSecret in the container and from
// user-secrets locally, never from appsettings (CLAUDE.md, Configuration & secrets). The
// rules that depend on the environment live in AuthOptionsValidator rather than in
// attributes, because DevelopmentBypass changes what is required.
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>The provider's issuer. Google by default; any OpenID Connect provider works.</summary>
    public Uri Authority { get; set; } = new("https://accounts.google.com");

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Who may sign in. Empty means anyone the provider vouches for, with a verified address.
    /// </summary>
    public string[] AllowedEmails { get; set; } = [];

    /// <summary>
    /// The address that takes over the library that existed before users did, on its first
    /// sign-in. Empty when there is none to adopt.
    /// </summary>
    public string InitialOwnerEmail { get; set; } = string.Empty;

    /// <summary>How long a session lasts without being used. Each use extends it.</summary>
    public TimeSpan SessionLifetime { get; set; } = TimeSpan.FromDays(30);

    /// <summary>
    /// Development only: every request is the development user, and no provider is configured. Startup
    /// refuses it in any other environment.
    /// </summary>
    public bool DevelopmentBypass { get; set; }
}
