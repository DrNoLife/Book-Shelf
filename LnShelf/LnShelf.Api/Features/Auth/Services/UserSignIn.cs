using System.Security.Claims;

namespace LnShelf.Api.Features.Auth.Services;

/// <summary>
/// Turns a provider's verified identity into this app's user: finds the user, adopts the
/// library from before users existed, or creates a new one, and returns the session's
/// principal.
/// </summary>
// Runs inside the OpenID Connect callback, before any cookie is issued, so an identity it
// refuses never holds a session. Open sign-up by default (feature-spec §7); Auth:AllowedEmails
// narrows it to a list when set.
public sealed class UserSignIn(
    LnShelfDbContext dbContext,
    IOptions<AuthOptions> options,
    IAvatarCommandService avatarCommand,
    TimeProvider timeProvider,
    ILogger<UserSignIn> logger)
{
    /// <returns>The session principal, or null when this identity may not sign in.</returns>
    public async Task<ClaimsPrincipal?> SignInAsync(ClaimsPrincipal identity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);

        var issuer = identity.FindFirst("iss")?.Value;
        var subject = identity.FindFirst("sub")?.Value;
        var email = identity.FindFirst("email")?.Value?.Trim();

        // Verified too, not only present: a provider can return an address the account holder
        // never proved they own, and the address is what adopts the initial owner's library.
        var verified = string.Equals(
            identity.FindFirst("email_verified")?.Value, "true", StringComparison.OrdinalIgnoreCase);

        if (issuer is null || subject is null || email is null || !verified || !IsAllowed(email))
        {
            logger.LogWarning("Sign-in refused for {Email}: verified {Verified}", email, verified);
            return null;
        }

        var now = timeProvider.GetUtcNow();
        var user = await dbContext.Users
                       .FirstOrDefaultAsync(
                           candidate => candidate.Issuer == issuer && candidate.Subject == subject,
                           cancellationToken)
                   ?? await AdoptInitialOwnerAsync(email, cancellationToken)
                   ?? CreateUser(now);

        user.Issuer = issuer;
        user.Subject = subject;
        user.Email = email;
        user.LastSeenAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        // The picture is the one thing taken from the "profile" scope. The name arrives with it
        // and is not read, so it is never stored. Best effort, after the user is saved: a
        // picture that fails to arrive costs the avatar, never the sign-in.
        await avatarCommand.RefreshProviderCopyAsync(user, identity.FindFirst("picture")?.Value, cancellationToken);

        return Principal(user.Id, email);
    }

    /// <summary>The claims every session carries, whichever way it was signed in.</summary>
    public static ClaimsPrincipal Principal(Guid userId, string email) =>
        new(new ClaimsIdentity(
            [
                new Claim(LnShelfClaims.UserId, userId.ToString()),
                new Claim("email", email),
            ],
            authenticationType: "LnShelf",
            nameType: "email",
            roleType: null));

    private bool IsAllowed(string email)
    {
        var allowed = options.Value.AllowedEmails;
        return allowed.Length == 0
               || allowed.Any(entry => string.Equals(entry.Trim(), email, StringComparison.OrdinalIgnoreCase));
    }

    // The library from before users existed belongs to the placeholder row the migration made.
    // The configured address takes it over in place, so no row is re-pointed.
    private async Task<User?> AdoptInitialOwnerAsync(string email, CancellationToken cancellationToken)
    {
        var initialOwnerEmail = options.Value.InitialOwnerEmail.Trim();
        if (initialOwnerEmail.Length == 0
            || !string.Equals(initialOwnerEmail, email, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var placeholder = await dbContext.Users.FirstOrDefaultAsync(
            candidate => candidate.Id == User.InitialOwnerPlaceholderId && candidate.Issuer == User.LocalIssuer,
            cancellationToken);

        if (placeholder is not null)
        {
            logger.LogInformation("{Email} adopted the library that existed before users", email);
        }

        return placeholder;
    }

    private User CreateUser(DateTimeOffset now)
    {
        var user = new User { Id = Guid.CreateVersion7(), CreatedAt = now };
        dbContext.Users.Add(user);
        logger.LogInformation("New user {UserId}", user.Id);
        return user;
    }
}
