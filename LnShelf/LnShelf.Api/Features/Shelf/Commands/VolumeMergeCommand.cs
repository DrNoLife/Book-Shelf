using LnShelf.Api.Commands;

namespace LnShelf.Api.Features.Shelf.Commands;

/// <summary>
/// <c>dotnet run --project LnShelf/LnShelf.Api -- volume-merge --series "&lt;title&gt;" --from 10 --into 7.5 [--format LightNovel] [--dry-run]</c>
/// </summary>
// A one-shot, not an endpoint: it repairs phantom volumes made from a source's count before
// bonus volumes existed (Torture Princess "volume 10", marked read meaning Vol. 7.5). That is
// a handful of rows, once, and does not earn a permanent hole in the API.
public sealed class VolumeMergeCommand(IVolumeMergeCommandService merge) : IConsoleCommand
{
    public string Name => "volume-merge";

    public string Description =>
        "Moves one volume's user state onto another of the same series and deletes the first. --dry-run only reports.";

    public async Task<int> RunAsync(ConsoleCommandLine commandLine, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(commandLine);

        try
        {
            var report = await merge.MergeAsync(ParseRequest(commandLine), cancellationToken);

            // Printed as well as logged: a one-shot's whole output is the line the person who
            // ran it reads.
            Console.WriteLine(report.Summary);
            return 0;
        }
        catch (InvalidInputException exception)
        {
            // A refusal is an answer, not a failure: the person gets the reason and exit code 2,
            // the same code the runner gives for a command line it cannot use.
            await Console.Error.WriteLineAsync(exception.Message);
            return 2;
        }
    }

    private static VolumeMergeRequest ParseRequest(ConsoleCommandLine commandLine)
    {
        var title = commandLine.Option("series");
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new InvalidInputException(
                "Usage: volume-merge --series \"<title>\" --from <number> --into <number> [--format <format>] [--dry-run]");
        }

        return new VolumeMergeRequest(
            title,
            ParseFormat(commandLine.Option("format")),
            ParseNumber(commandLine.Option("from"), "from"),
            ParseNumber(commandLine.Option("into"), "into"),
            DryRun: commandLine.Arguments.Contains("--dry-run", StringComparer.OrdinalIgnoreCase));
    }

    // Invariant, so "7.5" means seven and a half on a Danish machine too, where the current
    // culture would read it as seventy-five.
    private static decimal ParseNumber(string? value, string optionName) =>
        decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number)
            ? number
            : throw new InvalidInputException($"--{optionName} needs a volume number such as 10 or 7.5.");

    private static Format? ParseFormat(string? value)
    {
        if (value is null)
        {
            return null;
        }

        return Enum.TryParse<Format>(value, ignoreCase: true, out var format)
            ? format
            : throw new InvalidInputException(
                $"'{value}' is not a format. Try {string.Join(", ", Enum.GetNames<Format>())}.");
    }
}
