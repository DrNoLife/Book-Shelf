using LnShelf.Api.Commands;

namespace LnShelf.Api.Features.Covers.Commands;

/// <summary>
/// <c>dotnet run --project LnShelf/LnShelf.Api -- cover-orphan-sweep [--dry-run]</c>
/// </summary>
public sealed class CoverOrphanSweepCommand(CoverOrphanSweeper sweeper) : IConsoleCommand
{
    public string Name => "cover-orphan-sweep";

    public string Description =>
        "Deletes uploaded covers whose volume no longer exists, and avatars no account points at. --dry-run only counts them. Safe to re-run.";

    // Whether a cover is orphaned depends on every shelf, so the runner picks no user: the
    // sweeper acts as each user in turn, in a scope of its own.
    public bool ActsOnOneShelf => false;

    public async Task<int> RunAsync(ConsoleCommandLine commandLine, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(commandLine);

        var dryRun = commandLine.Arguments.Contains("--dry-run", StringComparer.OrdinalIgnoreCase);
        var report = await sweeper.RunAsync(dryRun, cancellationToken);

        // Printed as well as logged: a one-shot's whole output is the line the person who ran
        // it reads.
        Console.WriteLine(report.Summary);

        return report.Failed is 0 ? 0 : 1;
    }
}
