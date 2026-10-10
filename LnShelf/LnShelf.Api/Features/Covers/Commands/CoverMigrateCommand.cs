using LnShelf.Api.Commands;

namespace LnShelf.Api.Features.Covers.Commands;

/// <summary>
/// <c>dotnet LnShelf.Api.dll cover-migrate [--dry-run]</c>: copies every cover out of the retired
/// MinIO store into the cover folder.
/// </summary>
// A one-release command: it goes when MinIO does. Run before the new release takes traffic, so
// there is no window in which the shelf asks the empty folder for covers (README.md, Deploying).
public sealed class CoverMigrateCommand(
    [FromKeyedServices(CoversFeatureExtensions.MinioServiceKey)] ICoverStore source,
    ICoverStore target,
    CoverMigrator migrator) : IConsoleCommand
{
    public string Name => "cover-migrate";

    public string Description =>
        "Copies every cover from MinIO into the cover folder, verifying each. --dry-run only counts. Safe to re-run.";

    // Covers belong to the store, not to a shelf.
    public bool ActsOnOneShelf => false;

    public async Task<int> RunAsync(ConsoleCommandLine commandLine, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(commandLine);

        var dryRun = commandLine.Arguments.Contains("--dry-run", StringComparer.OrdinalIgnoreCase);
        var report = await migrator.RunAsync(source, target, dryRun, cancellationToken);

        Console.WriteLine(report.Summary);

        return report.Failed is 0 ? 0 : 1;
    }
}
