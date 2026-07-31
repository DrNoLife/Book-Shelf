using LnShelf.Api.Commands;

namespace LnShelf.Api.Features.Metadata.Commands;

/// <summary>
/// <c>dotnet run --project LnShelf/LnShelf.Api -- isbn-apply [--in &lt;path&gt;]</c>
/// </summary>
// A separate command from isbn-discover on purpose. Discovery guesses, and its
// measured hit rate on unknown ISBNs was poor enough that a person has to look at the
// guesses before any of them becomes shelf data.
public sealed class IsbnApplyCommand(IsbnReviewFile reviewFile, IsbnApplyService apply) : IConsoleCommand
{
    /// <summary>Matches <c>IsbnDiscoverCommand</c>'s default: the working directory.</summary>
    private const string DefaultPath = "isbn-review.csv";

    public string Name => "isbn-apply";

    public string Description =>
        "Writes the ISBNs left in a reviewed CSV onto their volumes. Safe to re-run.";

    public async Task<int> RunAsync(ConsoleCommandLine commandLine, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(commandLine);

        var path = commandLine.Option("in") ?? DefaultPath;

        var instructions = await reviewFile.ReadAsync(path, cancellationToken);
        var report = await apply.ApplyAsync(instructions, cancellationToken);

        Console.WriteLine(report.Summary);

        return 0;
    }
}
