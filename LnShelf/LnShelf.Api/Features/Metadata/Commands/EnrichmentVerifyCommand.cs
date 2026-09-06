using LnShelf.Api.Commands;

namespace LnShelf.Api.Features.Metadata.Commands;

/// <summary>
/// <c>dotnet run --project LnShelf/LnShelf.Api -- enrichment-verify [--series "A;B"] [--format LightNovel]</c>
/// </summary>
public sealed class EnrichmentVerifyCommand(EnrichmentVerificationService verification) : IConsoleCommand
{
    /// <summary>Semicolons, not commas: series titles contain commas and rarely semicolons.</summary>
    private const char SeriesSeparator = ';';

    public string Name => "enrichment-verify";

    public string Description =>
        "Checks live that a title, description and cover can still be fetched for a series and its volumes.";

    public async Task<int> RunAsync(ConsoleCommandLine commandLine, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(commandLine);

        var report = await verification.RunAsync(
            SearchTerms(commandLine), ParseFormat(commandLine), cancellationToken);

        foreach (var line in EnrichmentVerificationConsoleReport.Render(report))
        {
            Console.WriteLine(line);
        }

        // The exit code is the machine-readable half of the answer: a check nobody can put
        // behind a `&&` is a check that quietly stops being run.
        return report.Passed ? 0 : 1;
    }

    private static IReadOnlyList<string> SearchTerms(ConsoleCommandLine commandLine)
    {
        if (commandLine.Option("series") is not { } series)
        {
            return EnrichmentVerificationService.DefaultSearchTerms;
        }

        string[] terms =
        [
            .. series.Split(SeriesSeparator, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
        ];

        return terms.Length is 0
            ? throw new InvalidInputException("--series was given but named no series.")
            : terms;
    }

    private static Format ParseFormat(ConsoleCommandLine commandLine)
    {
        if (commandLine.Option("format") is not { } format)
        {
            return EnrichmentVerificationService.DefaultFormat;
        }

        return Enum.TryParse<Format>(format, ignoreCase: true, out var parsed)
            ? parsed
            : throw new InvalidInputException(
                $"'{format}' is not a format. Try {string.Join(", ", Enum.GetNames<Format>())}.");
    }
}
