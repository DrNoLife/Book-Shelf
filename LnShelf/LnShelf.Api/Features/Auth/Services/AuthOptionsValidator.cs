namespace LnShelf.Api.Features.Auth.Services;

/// <summary>
/// Refuses to start with a login that would not work, or with the development bypass
/// anywhere but Development.
/// </summary>
// A failed boot is the point: a missing client secret otherwise surfaces as a broken
// redirect at the first login, and a bypass switched on in production would be an open
// app that looks like a closed one.
public sealed class AuthOptionsValidator(IHostEnvironment environment) : IValidateOptions<AuthOptions>
{
    public ValidateOptionsResult Validate(string? name, AuthOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.DevelopmentBypass)
        {
            return environment.IsDevelopment()
                ? ValidateOptionsResult.Success
                : ValidateOptionsResult.Fail(
                    $"Auth:DevelopmentBypass is only allowed in Development, not in '{environment.EnvironmentName}'.");
        }

        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(options.ClientId)) missing.Add("Auth:ClientId");
        if (string.IsNullOrWhiteSpace(options.ClientSecret)) missing.Add("Auth:ClientSecret");

        if (missing.Count > 0)
        {
            return ValidateOptionsResult.Fail(
                $"Login is not configured: set {string.Join(", ", missing)} (README.md, Configuration).");
        }

        return options.SessionLifetime > TimeSpan.Zero
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail("Auth:SessionLifetime must be longer than zero.");
    }
}
