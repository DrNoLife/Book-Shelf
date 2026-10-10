namespace LnShelf.Api.Commands;

/// <summary>
/// A one-shot maintenance job the API can be started as instead of serving:
/// <c>dotnet run --project LnShelf/LnShelf.Api -- &lt;name&gt;</c>.
/// </summary>
// The named seam for work that runs once, or on demand, and has no business being a
// permanent hole in the API surface (feature-spec §6 lists no admin endpoints, and a
// backfill does not earn the first one). Implementations live in the slice whose data
// they touch, and that slice's Add<Feature>() registers them.
public interface IConsoleCommand
{
    /// <summary>The name typed after the <c>--</c>.</summary>
    string Name { get; }

    /// <summary>One line, printed when the name given matches nothing.</summary>
    string Description { get; }

    /// <summary>
    /// Whether the command works on one user's shelf, so the runner makes it act as that
    /// user (<c>--user</c>, or the only user). False for a command that looks across every
    /// shelf and so needs none.
    /// </summary>
    bool ActsOnOneShelf => true;

    /// <returns>The process exit code. Zero is success.</returns>
    Task<int> RunAsync(ConsoleCommandLine commandLine, CancellationToken cancellationToken);
}
