using LnShelf.Api.Features.Metadata.Models.MangaDex;

namespace LnShelf.Api.Features.Metadata.Services;

/// <inheritdoc />
// MangaDex behind IChapterMapSource. Finds the record by title search and link (the series'
// remembered MangaDexId skips that), refuses a record that is not Japanese, and answers with
// the merged English/all-languages aggregate.
//
// Scoped, and the request allowance is a field: a scope is one sync run (the nightly loop and
// "Sync now" each make their own) or one on-demand refresh, so the allowance is per run without
// the run having to carry a third budget beside EnrichmentBudget's two.
public sealed class MangaDexChapterMapSource(
    MangaDexClient client,
    IOptions<MangaDexOptions> options,
    ILogger<MangaDexChapterMapSource> logger) : IChapterMapSource
{
    /// <summary>Two title searches and two aggregates: the most one series can cost.</summary>
    private const int MaximumRequestsPerSeries = 4;

    private const string JapaneseLanguage = "ja";

    private readonly LookupBudget _budget = new(options.Value.MaximumRequestsPerRun);

    public MetadataSourceName SourceName => MetadataSourceName.MangaDex;

    public async Task<ChapterMapAnswer> GetChapterMapAsync(Series series, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(series);

        // All or nothing per series: a half-asked series would be judged on half an answer.
        if (_budget.Remaining < MaximumRequestsPerSeries)
        {
            return ChapterMapAnswer.Skipped;
        }

        if (series.MangaDexId is not { } id)
        {
            var record = await FindAsync(series, cancellationToken);

            if (record is null)
            {
                return ChapterMapAnswer.Unmatched("No MangaDex record links to this series' AniList or MangaUpdates id.");
            }

            // Not remembered: a manhwa's or manhua's "volumes" are seasons, never the shelf's
            // editions, and asking again in a month costs one search.
            if (record.Attributes?.OriginalLanguage is not JapaneseLanguage)
            {
                return ChapterMapAnswer.Rejected(
                    $"The MangaDex record's original language is \"{record.Attributes?.OriginalLanguage}\", not Japanese.");
            }

            id = record.Id;
            series.MangaDexId = id;
        }

        var english = await client.GetAggregateAsync(id, englishOnly: true, cancellationToken);
        var allLanguages = await client.GetAggregateAsync(id, englishOnly: false, cancellationToken);
        _budget.Spend(2);

        return ChapterMapAnswer.Found(MangaDexAggregateMapper.ToChaptersByVolume(english, allLanguages));
    }

    /// <summary>
    /// Searches by romaji, then by English title, and keeps only a result linked to this series.
    /// </summary>
    private async Task<MangaDexManga?> FindAsync(Series series, CancellationToken cancellationToken)
    {
        string?[] titles = [series.TitleRomaji, series.TitleEnglish];

        foreach (var title in titles.Where(title => !string.IsNullOrWhiteSpace(title)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var results = await client.SearchAsync(title!, cancellationToken);
            _budget.Spend(1);

            if (MangaDexSeriesMatcher.Pick(results?.Data ?? [], series.AniListId, series.MangaUpdatesId) is { } match)
            {
                logger.LogInformation(
                    "Matched {TitleEnglish} to MangaDex {MangaDexId} ({OriginalLanguage})",
                    series.TitleEnglish,
                    match.Id,
                    match.Attributes?.OriginalLanguage);

                return match;
            }
        }

        return null;
    }
}
