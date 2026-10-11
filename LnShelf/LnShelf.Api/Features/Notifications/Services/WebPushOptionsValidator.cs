namespace LnShelf.Api.Features.Notifications.Services;

/// <summary>
/// Refuses to start with Web Push half set up. None of the three set is fine: that is the
/// feature switched off.
/// </summary>
// A failed boot is the point, as with AuthOptionsValidator: a mistyped key otherwise surfaces
// as every push failing quietly at 04:00, and a Subject Apple will not accept as every iPhone
// silently getting nothing.
public sealed class WebPushOptionsValidator : IValidateOptions<WebPushOptions>
{
    public ValidateOptionsResult Validate(string? name, WebPushOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var publicKey = options.PublicKey.NullIfBlank();
        var privateKey = options.PrivateKey.NullIfBlank();
        var subject = options.Subject.NullIfBlank();

        if (publicKey is null && privateKey is null && subject is null)
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();

        if (!VapidKeys.IsPublicKey(publicKey))
        {
            failures.Add("WebPush:PublicKey must be a P-256 public key in base64url (vapid-keys prints one).");
        }

        if (!VapidKeys.DecodesTo(privateKey, VapidKeys.PrivateKeyLength))
        {
            failures.Add("WebPush:PrivateKey must be a P-256 private key in base64url (vapid-keys prints one).");
        }

        if (subject is null
            || !(subject.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
                 || subject.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
        {
            failures.Add("WebPush:Subject must be a mailto: or https: address push services can reach you at.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
