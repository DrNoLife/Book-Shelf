using LnShelf.Api.Commands;

namespace LnShelf.Api.Features.Notifications.Commands;

/// <summary>
/// <c>dotnet run --project LnShelf/LnShelf.Api -- vapid-keys</c>
/// </summary>
// A command rather than a script, so the keys come from the same .NET crypto the server signs
// with, and the owner needs nothing installed beyond the image that already runs.
public sealed class VapidKeysCommand : IConsoleCommand
{
    public string Name => "vapid-keys";

    public string Description =>
        "Prints a fresh Web Push key pair for the server's .env. Replacing a pair in use turns notifications off on every device until it is turned on again.";

    // Touches no shelf: it only prints.
    public bool ActsOnOneShelf => false;

    public Task<int> RunAsync(ConsoleCommandLine commandLine, CancellationToken cancellationToken)
    {
        var keys = VapidKeys.Create();

        // Printed, never logged: the private key is a secret, and logs are kept.
        Console.WriteLine($"WEBPUSH_PUBLIC_KEY={keys.PublicKey}");
        Console.WriteLine($"WEBPUSH_PRIVATE_KEY={keys.PrivateKey}");
        Console.WriteLine("WEBPUSH_SUBJECT=mailto:you@example.com");

        return Task.FromResult(0);
    }
}
