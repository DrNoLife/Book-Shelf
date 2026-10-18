using LnShelf.Api.Features.Shelf.Models;
using LnShelf.Shared.Metadata;

namespace LnShelf.EndToEnd.Tests.Fakes;

/// <summary>
/// The chapter map, answered without MangaDex: Chainsaw Man's first thirteen volumes as the real
/// aggregate places them, and nothing for any other series. No journey ever reaches MangaDex.
/// </summary>
// Registered so that a sync a journey sets off (adding a series runs one) stays off the network,
// and so that one which reaches Chainsaw Man writes the same ranges the chapter journey seeds.
// Stateless, like the other fakes: the answer is a function of the series alone.
public sealed class FixtureChapterMapSource : IChapterMapSource
{
    /// <summary>The first chapter of each of Chainsaw Man's volumes 1–13, and where 13 ends.</summary>
    private static readonly decimal[] VolumeStarts = [1m, 8m, 17m, 26m, 35m, 44m, 53m, 62m, 71m, 80m, 89m, 98m, 104m, 113m];

    public MetadataSourceName SourceName => MetadataSourceName.MangaDex;

    /// <summary>No allowance to spend: the fake never reaches the network.</summary>
    public bool CanAnswer(Series series) => true;

    public Task<ChapterMapAnswer> GetChapterMapAsync(Series series, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(series);

        if (series.TitleEnglish != "Chainsaw Man")
        {
            return Task.FromResult(ChapterMapAnswer.Unmatched("Not a series the fake knows."));
        }

        var chaptersByVolume = new Dictionary<int, IReadOnlyList<decimal>>();
        for (var index = 0; index < VolumeStarts.Length - 1; index++)
        {
            var first = (int)VolumeStarts[index];
            var next = (int)VolumeStarts[index + 1];
            chaptersByVolume[index + 1] = [.. Enumerable.Range(first, next - first).Select(chapter => (decimal)chapter)];
        }

        return Task.FromResult(ChapterMapAnswer.Found(chaptersByVolume));
    }
}
