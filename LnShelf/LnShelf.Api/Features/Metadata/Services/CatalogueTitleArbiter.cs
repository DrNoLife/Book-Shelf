namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// One request per title tried, until the store recognises one — then that title's answer,
/// matched back onto the volumes the shelf tracks, and the winning title remembered.
/// </summary>
// The store arbitrates, and nothing here picks a title on its own. It cannot: the shelf
// holds whichever name the resolving source filed the series under, which is often romaji
// ("Kono Subarashii Sekai ni Shukufuku o!"), and the US storefront answers 0 results for
// those — measured, Konosuba enriched 0 of 17 volumes on that title while a series whose
// stored title happened to be English enriched 28 of 28. The English name is in the
// alternatives, unlabelled, next to the Russian and Arabic ones, and no cheap rule can tell
// which is which (Torture Princess' bag holds "Isekai Goumon Hime", "Isekai Goumon-hime"
// and "Torture Princess: Fremd Torturchen" — any rule picks wrong on one). Whether a
// storefront recognises a title is a fact only the storefront has, so it is asked, and the
// winner is remembered on the series (Series.CatalogueTitle).
//
// "Recognises" means at least one of this series' tracked volumes confirmed by the matcher,
// not a non-empty response: a store answers 74 results for "Gifting this Wonderful World
// with Blessings!" and not one of them is the series.
public sealed class CatalogueTitleArbiter(
    IVolumeCatalogueSource catalogueSource,
    CatalogueVolumeMatcher matcher,
    ILogger<CatalogueTitleArbiter> logger)
{
    /// <returns>
    /// The record for each tracked volume the store confirmed, empty when no title was
    /// recognised.
    /// </returns>
    public async Task<IReadOnlyDictionary<int, CatalogueVolume>> AskAsync(
        Series series,
        SyncRun run,
        LookupBudget budget,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(budget);

        var attempts = CatalogueAttempts(series);
        var remembered = series.CatalogueTitle;
        var rememberedAnswered = false;
        var tried = 0;

        for (var attempt = 0; attempt < attempts.Length; attempt++)
        {
            if (budget.IsExhausted)
            {
                break;
            }

            // Spent from the same budget the by-ISBN lookups draw on, which is deliberate
            // for now and is the one thing about it worth knowing: a series with no
            // remembered title can cost several of a run's allowance before a single ISBN
            // is looked up. See the note on OpenLibraryOptions.MaximumLookupsPerRun.
            budget.Spend(1);
            tried++;

            var catalogue = await TryGetVolumesAsync(series, attempts[attempt], run, cancellationToken);

            if (catalogue is null)
            {
                continue;
            }

            rememberedAnswered |= string.Equals(attempts[attempt], remembered, StringComparison.Ordinal);

            var matched = matcher.Match(series, attempts[attempt], catalogue).ByVolumeNumber;

            logger.LogInformation(
                "{SourceName} matched {MatchedCount} of {RecordCount} records to {TitleEnglish}'s volumes "
                + "under \"{AttemptTitle}\"",
                catalogueSource.SourceName,
                matched.Count,
                catalogue.Count,
                series.TitleEnglish,
                attempts[attempt]);

            if (matched.Count is 0)
            {
                continue;
            }

            // Learned, so the next sweep spends one request rather than this many. Assigning
            // the same string it already held changes no row — EF's change tracking decides
            // that — so the ordinary case stays a no-op write.
            series.CatalogueTitle = attempts[attempt];

            logger.LogInformation(
                "{TitleEnglish}: the {SourceName} catalogue answered under \"{CatalogueTitle}\", "
                + "attempt {Attempt} of {AttemptCount}",
                series.TitleEnglish,
                catalogueSource.SourceName,
                attempts[attempt],
                attempt + 1,
                attempts.Length);

            return matched;
        }

        ForgetCatalogueTitleIfStale(series, tried, attempts.Length, rememberedAnswered);

        return new Dictionary<int, CatalogueVolume>();
    }

    /// <summary>
    /// The titles to try, in order: the one the store already answered to, then the shelf's
    /// own, then every other title a source knows this series by.
    /// </summary>
    // The remembered title leads so the ordinary night costs exactly one request, and the
    // rest of the list still follows it — which is what makes a remembered title that has
    // stopped working self-healing rather than permanent.
    //
    // Latin-filtered again here, defensively rather than because the column is untrusted: a
    // row written before the filter existed, or edited by hand in psql, would otherwise
    // spend a request on kanji the US store cannot match a character of.
    private static string[] CatalogueAttempts(Series series) =>
        SeriesKnownTitles.Askable(
            [series.CatalogueTitle, series.TitleEnglish, .. series.AlternativeTitles]);

    /// <returns>Null when the store could not be reached, which is not an answer.</returns>
    // Failure isolation per attempt, matching what the per-volume loop does. A store being
    // down is not a reason for the series to lose its ISBN lookups too — nor for one title's
    // failed request to cost the other titles theirs.
    private async Task<IReadOnlyList<CatalogueVolume>?> TryGetVolumesAsync(
        Series series,
        string attemptTitle,
        SyncRun run,
        CancellationToken cancellationToken)
    {
        try
        {
            return await catalogueSource.GetVolumesAsync(attemptTitle, series.Format, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            run.EnrichmentFailures++;
            logger.LogWarning(
                exception,
                "The {SourceName} catalogue failed for {TitleEnglish} under \"{AttemptTitle}\"; continuing",
                catalogueSource.SourceName,
                series.TitleEnglish,
                attemptTitle);

            return null;
        }
    }

    /// <summary>
    /// Clears a remembered title the store has stopped answering to, so the next run
    /// re-learns from scratch.
    /// </summary>
    /// <param name="triedCount">
    /// How many of the titles were actually asked about. Fewer than all of them means the
    /// budget ran out first, which disproves nothing.
    /// </param>
    /// <param name="rememberedAnswered">
    /// Whether the store actually answered under it. A store that could not be reached has
    /// said nothing about the title, and an outage must not erase what the shelf learned.
    /// </param>
    // Cleared rather than kept, because a learned link that no longer resolves is worse than
    // none: it would go on being tried first every night. The remembered title is always the
    // first attempt and the pass never starts with an exhausted budget, so by the time this
    // runs it has certainly had its turn — but only a finished list and a real answer count
    // as evidence against it. Clearing on a budget cut or an outage would trade one wasted
    // request for a whole attempt list on the next run.
    private void ForgetCatalogueTitleIfStale(
        Series series,
        int triedCount,
        int attemptCount,
        bool rememberedAnswered)
    {
        if (series.CatalogueTitle is { } remembered && rememberedAnswered && triedCount == attemptCount)
        {
            logger.LogWarning(
                "{TitleEnglish}: the {SourceName} catalogue no longer answers under \"{CatalogueTitle}\", "
                + "nor under any of the {AttemptCount} titles tried; forgetting it",
                series.TitleEnglish,
                catalogueSource.SourceName,
                remembered,
                attemptCount);

            series.CatalogueTitle = null;
            return;
        }

        logger.LogInformation(
            "{TitleEnglish}: the {SourceName} catalogue recognised none of the {TriedCount} of "
            + "{AttemptCount} titles tried",
            series.TitleEnglish,
            catalogueSource.SourceName,
            triedCount,
            attemptCount);
    }
}
