using LnShelf.Api.Commands;

namespace LnShelf.Api.Features.Metadata.Commands;

/// <summary>
/// <c>dotnet run --project LnShelf/LnShelf.Api -- isbn-discover [--out &lt;path&gt;]</c>
/// </summary>
public sealed class IsbnDiscoverCommand(IsbnDiscoveryService discovery, IsbnReviewFile reviewFile)
    : IConsoleCommand
{
    /// <summary>
    /// Beside whatever the working directory is, with no folder assumed.
    /// </summary>
    // `dotnet run --project` runs with the *project* as the working directory, so a
    // default of "docs/…" would quietly write into LnShelf.Api/docs rather than the
    // repository's. Pass --out for anywhere else.
    private const string DefaultPath = "isbn-review.csv";

    public string Name => "isbn-discover";

    public string Description =>
        "Proposes ISBNs for owned volumes that have none, into a CSV for review. Writes nothing to the shelf.";

    public async Task<int> RunAsync(ConsoleCommandLine commandLine, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(commandLine);

        var path = commandLine.Option("out") ?? DefaultPath;

        var rows = await discovery.RunAsync(cancellationToken);
        await reviewFile.WriteAsync(path, rows, cancellationToken);

        var proposed = rows.Count(row => row.Isbn is not null);

        Console.WriteLine(
            $"{rows.Count} volumes without an ISBN · {proposed} proposed · "
            + $"{rows.Count - proposed} left for you");
        Console.WriteLine($"Review {Path.GetFullPath(path)}, delete the rows you do not believe, then:");
        Console.WriteLine($"  dotnet run --project LnShelf/LnShelf.Api -- isbn-apply --in \"{Path.GetFullPath(path)}\"");

        return 0;
    }
}
