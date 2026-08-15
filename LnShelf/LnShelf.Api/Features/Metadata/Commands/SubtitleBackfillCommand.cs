using LnShelf.Api.Commands;

namespace LnShelf.Api.Features.Metadata.Commands;

/// <summary>
/// <c>dotnet run --project LnShelf/LnShelf.Api -- subtitle-backfill</c>
/// </summary>
// A one-shot rather than a change to the nightly pass, for the reason SubtitleBackfillService
// sets out: an empty title is a resolved answer the sync has to go on trusting, and the only
// honest way to ask a second source about it is to ask deliberately, once.
public sealed class SubtitleBackfillCommand(SubtitleBackfillService backfill) : IConsoleCommand
{
    public string Name => "subtitle-backfill";

    public string Description =>
        "Asks the by-ISBN source for a subtitle for volumes a catalogue named without one. Safe to re-run.";

    public async Task<int> RunAsync(ConsoleCommandLine commandLine, CancellationToken cancellationToken)
    {
        var report = await backfill.RunAsync(cancellationToken);

        // Printed as well as logged: a one-shot's whole output is the lines the person who
        // ran it reads. Per series, because that is the grain a person checks it at — "86 got
        // its subtitles back" is the question being asked.
        foreach (var series in report.Series)
        {
            Console.WriteLine(series.Summary);
        }

        Console.WriteLine(report.Summary);

        return 0;
    }
}
