using LnShelf.Api.Commands;

namespace LnShelf.Api.Features.Covers.Commands;

/// <summary>
/// <c>dotnet run --project LnShelf/LnShelf.Api -- community-cover-withdraw --hash &lt;hash&gt;</c>
/// </summary>
public sealed class CommunityCoverWithdrawCommand(ICommunityCoverCommandService commandService) : IConsoleCommand
{
    public string Name => "community-cover-withdraw";

    public string Description =>
        "Stops sharing a community cover with every user, by its content hash (--hash). Copies users already picked stay on their shelves.";

    // Moderation reaches across every user's published covers, so there is no shelf to act as.
    public bool ActsOnOneShelf => false;

    public async Task<int> RunAsync(ConsoleCommandLine commandLine, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(commandLine);

        var hash = commandLine.Option("hash")?.Trim().ToLowerInvariant();
        if (hash is null || hash.Length != CoverContentHash.Length || !hash.All(char.IsAsciiHexDigit))
        {
            Console.WriteLine($"--hash needs the {CoverContentHash.Length}-character content hash of the cover.");
            return 1;
        }

        var withdrawn = await commandService.WithdrawAsync(hash, cancellationToken);
        Console.WriteLine($"Withdrew {withdrawn} shared cover(s) with hash {hash}.");

        return 0;
    }
}
