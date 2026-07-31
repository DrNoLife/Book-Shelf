using LnShelf.Api.Commands;

namespace LnShelf.Api.Features.Import.Commands;

/// <summary>
/// <c>dotnet run --project LnShelf/LnShelf.Api -- isbn-backfill</c>
/// </summary>
public sealed class IsbnBackfillCommand(IsbnBackfillService backfill) : IConsoleCommand
{
    public string Name => "isbn-backfill";

    public string Description =>
        "Copies the ISBNs already in import staging onto the volumes they identify. Safe to re-run.";

    public async Task<int> RunAsync(ConsoleCommandLine commandLine, CancellationToken cancellationToken)
    {
        var report = await backfill.RunAsync(cancellationToken);

        // Printed as well as logged: a one-shot's whole output is the line the person
        // who ran it reads.
        Console.WriteLine(report.Summary);

        return 0;
    }
}
