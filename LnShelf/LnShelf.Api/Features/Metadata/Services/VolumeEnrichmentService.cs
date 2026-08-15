namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// Fills in a series' volumes from the two per-volume sources: the store catalogue first,
/// then a per-ISBN lookup for whatever is still missing.
/// </summary>
// Extracted from SeriesSyncService rather than added to it. That class's job is "walk the
// shelf, reconcile each series against the release source"; this one's is "which volumes
// are worth asking about, and who to ask". What may be written from an answer lives one
// step further out again, in VolumeFactWriter, because both passes here share those rules.
//
// The order of the two passes is the precedence order, and it is deliberate. The catalogue
// answers for a whole series in one request and carries the fields the ISBN lookup does
// not (a blurb, and a date for volumes with no ISBN at all); the ISBN lookup is
// unambiguous but only reaches the 59% of volumes whose ISBN is known. Catalogue first,
// gaps second.
public sealed class VolumeEnrichmentService(
    IVolumeCatalogueSource catalogueSource,
    IVolumeEnrichmentSource enrichmentSource,
    IIsbnCandidateSource candidateSource,
    VolumeFactWriter factWriter,
    ILogger<VolumeEnrichmentService> logger)
{
    /// <param name="today">
    /// "In the future" is relative to a day, so the caller supplies it — the same
    /// convention the projections follow.
    /// </param>
    /// <param name="budget">
    /// Spent down as requests are made, not as volumes are visited: the catalogue costs one
    /// for the whole series, and one volume can cost several when its own ISBN turns out
    /// not to be indexed.
    /// </param>
    public async Task EnrichAsync(
        Series series,
        SyncRun run,
        DateOnly today,
        LookupBudget budget,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(budget);

        await EnrichFromCatalogueAsync(series, run, today, budget, cancellationToken);
        await EnrichByIsbnAsync(series, run, today, budget, cancellationToken);
    }

    /// <summary>
    /// One request per title tried, until the store recognises one — then that title's
    /// answer, matched back onto the volumes the shelf tracks.
    /// </summary>
    // The store arbitrates, and nothing here picks a title on its own. It cannot: the shelf
    // holds whichever name the resolving source filed the series under, which is often
    // romaji ("Kono Subarashii Sekai ni Shukufuku o!"), and the US storefront answers 0
    // results for those — measured, Konosuba enriched 0 of 17 volumes on that title while a
    // series whose stored title happened to be English enriched 28 of 28. The English name
    // is in the alternatives, unlabelled, next to the Russian and Arabic ones, and no cheap
    // rule can tell which is which (Torture Princess' bag holds "Isekai Goumon Hime",
    // "Isekai Goumon-hime" and "Torture Princess: Fremd Torturchen" — any rule picks wrong
    // on one). Whether a storefront recognises a title is a fact only the storefront has, so
    // it is asked, and the winner is remembered on the series (Series.CatalogueTitle).
    //
    // "Recognises" means at least one of this series' tracked volumes confirmed by the
    // matcher, not a non-empty response: a store answers 74 results for "Gifting this
    // Wonderful World with Blessings!" and not one of them is the series.
    private async Task EnrichFromCatalogueAsync(
        Series series,
        SyncRun run,
        DateOnly today,
        LookupBudget budget,
        CancellationToken cancellationToken)
    {
        if (budget.IsExhausted || !series.Volumes.Any(NeedsAnything))
        {
            return;
        }

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

            budget.Spend(1);
            tried++;

            var catalogue = await TryGetVolumesAsync(series, attempts[attempt], run, cancellationToken);

            if (catalogue is null)
            {
                continue;
            }

            rememberedAnswered |= string.Equals(attempts[attempt], remembered, StringComparison.Ordinal);

            var matched = MatchToVolumeNumbers(series, attempts[attempt], catalogue);

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

            await ApplyMatchedAsync(series, matched, run, today, cancellationToken);

            return;
        }

        ForgetCatalogueTitleIfStale(series, tried, attempts.Length, rememberedAnswered);
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
    // Failure isolation per attempt, matching what the per-volume loop below does. A store
    // being down is not a reason for the series to lose its ISBN lookups too — nor for one
    // title's failed request to cost the other titles theirs.
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

    private async Task ApplyMatchedAsync(
        Series series,
        Dictionary<int, CatalogueVolume> matched,
        SyncRun run,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        foreach (var volume in series.Volumes.OrderBy(volume => volume.Number))
        {
            if (!matched.TryGetValue(volume.Number, out var record))
            {
                continue;
            }

            await factWriter.ApplyAsync(series, volume, ToFacts(record), run, today, cancellationToken);
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

    /// <summary>
    /// Which of the store's records is this series' volume N, where any is.
    /// </summary>
    /// <param name="seriesTitle">
    /// The title this answer was fetched under, not necessarily the shelf's own. It has to
    /// be the one the matcher compares against: when the store answered under an
    /// alternative, its records name *that* title — "Konosuba: God's Blessing on This
    /// Wonderful World!, Vol. 3 (light novel)" for a series the shelf calls "Kono Subarashii
    /// Sekai ni Shukufuku o!" — and the shelf's own title appears nowhere in them. The
    /// subtitle extractor needs no such care; it anchors on the volume number, not the title.
    /// </param>
    // Two filters, both of them the matcher's, and both necessary. A store search for
    // "Slime light novel" answers with the manga, the German editions, the spin-offs and
    // volumes of unrelated series that happen to share a word; TryMatchVolumeNumber
    // refuses everything whose title does not name this series with a number leading
    // straight after it, and ConfirmsMedium then insists the record say which medium it is
    // rather than merely fail to say it is the other one. This pass writes to the shelf
    // with nobody looking, which is the bar that earns the stricter of the two.
    //
    // First confirmed record per volume number wins. Apple lists a volume more than once —
    // a bundle, a re-release — and the ranking puts the plain edition first.
    //
    // Volume numbers the shelf does not track are ignored outright. The release source owns
    // volume counts (CLAUDE.md), so a store listing volume 25 of a series the count says
    // has 23 is not grounds to create two volumes; it is a store carrying a spin-off or a
    // future listing under a number this shelf has no opinion about.
    private Dictionary<int, CatalogueVolume> MatchToVolumeNumbers(
        Series series,
        string seriesTitle,
        IReadOnlyList<CatalogueVolume> catalogue)
    {
        var tracked = series.Volumes.Select(volume => volume.Number).ToHashSet();
        var matched = new Dictionary<int, CatalogueVolume>();

        foreach (var record in catalogue)
        {
            if (!VolumeEditionMatcher.TryMatchVolumeNumber(
                    seriesTitle, series.Format, record.Title, out var number))
            {
                continue;
            }

            if (!tracked.Contains(number) || matched.ContainsKey(number))
            {
                continue;
            }

            if (!VolumeEditionMatcher.ConfirmsMedium(series.Format, record.Title))
            {
                logger.LogInformation(
                    "{TitleEnglish} volume {VolumeNumber}: {RecordKey} (\"{RecordTitle}\") is the right "
                    + "volume but does not state that it is a {Format}; skipped",
                    series.TitleEnglish,
                    number,
                    record.SourceRecordKey,
                    record.Title,
                    series.Format);

                continue;
            }

            matched[number] = record;
        }

        logger.LogInformation(
            "{SourceName} matched {MatchedCount} of {RecordCount} records to {TitleEnglish}'s volumes "
            + "under \"{AttemptTitle}\"",
            catalogueSource.SourceName,
            matched.Count,
            catalogue.Count,
            series.TitleEnglish,
            seriesTitle);

        return matched;
    }

    private VolumeFacts ToFacts(CatalogueVolume record) => new()
    {
        Source = MetadataSourceName.AppleBooks,
        SourceName = catalogueSource.SourceName,
        SourceRecordKey = record.SourceRecordKey,
        Title = record.Title,
        Description = record.Description,
        ReleaseDate = record.ReleaseDate,

        // Nothing to keep: a storefront date is either a full ISO timestamp or absent, so
        // there is no coarse-but-real string of the "2022" kind to report.
        RawReleaseDate = null,
        CoverImageUrl = record.ArtworkUrl,
    };

    private static VolumeFacts ToFacts(VolumeEnrichment enrichment, string sourceName) => new()
    {
        Source = MetadataSourceName.OpenLibrary,
        SourceName = sourceName,
        SourceRecordKey = enrichment.SourceRecordKey,
        Title = enrichment.Title,

        // Open Library carries no blurbs for this shelf's editions at all — measured 0 of
        // 15 — so there is nothing to map and ShouldLookUp does not ask on their account.
        Description = null,
        ReleaseDate = enrichment.ReleaseDate,
        RawReleaseDate = enrichment.RawPublishDate,
        CoverImageUrl = enrichment.CoverImageUrl,
    };

    private async Task EnrichByIsbnAsync(
        Series series,
        SyncRun run,
        DateOnly today,
        LookupBudget budget,
        CancellationToken cancellationToken)
    {
        foreach (var volume in series.Volumes.OrderBy(volume => volume.Number))
        {
            if (budget.IsExhausted)
            {
                break;
            }

            if (!ShouldLookUp(volume))
            {
                continue;
            }

            // Per-volume failure isolation. The sync run already isolates per series;
            // this keeps one unreachable edition record from costing a series its other
            // twenty volumes.
            try
            {
                await ApplyByIsbnAsync(series, volume, run, today, budget, cancellationToken);
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
                    "Enrichment failed for {TitleEnglish} volume {VolumeNumber} (ISBN {Isbn}); continuing",
                    series.TitleEnglish,
                    volume.Number,
                    volume.Isbn);
            }
        }
    }

    /// <summary>Whether any source is still worth a request for this volume at all.</summary>
    private static bool NeedsAnything(Volume volume) =>
        volume.ReleaseDate is null
        || volume.CoverObjectKey is null
        || TitleUnresolved(volume)
        || volume.Description is null;

    /// <summary>
    /// Whether any source has named this volume yet.
    /// </summary>
    // The distinction Volume.Title's three states exist for: an empty title is an answer —
    // "named, and it has no subtitle" — and asking again would spend a request a night on a
    // question already settled. Only null is a gap.
    private static bool TitleUnresolved(Volume volume) => volume.Title is null;

    /// <summary>
    /// Whether this volume is worth a request to the by-ISBN source. Silent about the ones
    /// that are not.
    /// </summary>
    // Three reasons to skip, and none of them is an error:
    //
    // - No ISBN. The seam is keyed on ISBN because that is the only key that works; a
    //   volume without one has nothing to ask about. 41% of the owned shelf, and the
    //   discovery command is the answer to it, not this.
    // - A Japanese-market ISBN. It resolves to a Japanese-market edition, whose date is
    //   the Japanese release date and whose art is the Japanese cover — both wrong on an
    //   English shelf entry. Checked from the ISBN rather than from the record, because
    //   the record may state no language at all: Slime volume 1's Japanese ISBN resolves
    //   to a French edition with an empty `languages` field.
    // - Already complete. A volume with a date, a cover and a title has nothing left to
    //   learn, so a nightly run does not re-ask. Incomplete ones are re-asked, which is
    //   deliberate: Open Library's catalogue grows, and today's missing record is next
    //   quarter's hit.
    //
    // Description is deliberately absent from that last list, unlike NeedsAnything's.
    // Open Library carried a blurb for 0 of this shelf's 15 sampled ISBNs, so a volume
    // whose only gap is its description would be re-asked every night forever for a field
    // the source has never once supplied — sixty budgeted lookups a night spent on a
    // certainty.
    private static bool ShouldLookUp(Volume volume) =>
        volume.Isbn is not null
        && !Isbn.IsJapaneseEdition(volume.Isbn)
        && (volume.ReleaseDate is null || volume.CoverObjectKey is null || TitleUnresolved(volume));

    private async Task ApplyByIsbnAsync(
        Series series,
        Volume volume,
        SyncRun run,
        DateOnly today,
        LookupBudget budget,
        CancellationToken cancellationToken)
    {
        budget.Spend(1);
        var enrichment = await enrichmentSource.GetByIsbnAsync(volume.Isbn!, cancellationToken);

        // Not indexed under the ISBN this shelf holds — which does not mean the volume is
        // absent. The printing the reader owns is often simply not the printing a library
        // catalogued: five of Slime's ISBNs 404 while Open Library plainly holds those
        // volumes under other ISBNs. So the volume is searched for by name, and any
        // candidate has to prove it is this series' volume N before it is believed.
        enrichment ??= await ResolveBySearchAsync(series, volume, run, budget, cancellationToken);

        if (enrichment is null)
        {
            run.EnrichmentGaps++;
            return;
        }

        if (enrichment.Language is EditionLanguage.Other)
        {
            // The ISBN check above catches the Japanese-market ones; this catches the
            // rest — a Brazilian or French edition sharing a work with the English one.
            run.EnrichmentGaps++;
            logger.LogInformation(
                "Skipped {RecordKey} for {TitleEnglish} volume {VolumeNumber}: the record names a "
                + "language other than English",
                enrichment.SourceRecordKey,
                series.TitleEnglish,
                volume.Number);
            return;
        }

        run.VolumesEnriched++;

        await factWriter.ApplyAsync(
            series,
            volume,
            ToFacts(enrichment, enrichmentSource.SourceName),
            run,
            today,
            cancellationToken);
    }

    /// <summary>
    /// Finds another printing of the same volume, when the held ISBN is not indexed.
    /// </summary>
    // Held to a far higher bar than a direct ISBN lookup, and deliberately so. Asking for
    // an ISBN is unambiguous; searching by name is a guess, and this shelf has the worst
    // case of it — the Slime light novel and the Slime manga share a title. So a candidate
    // is only believed when its own record names this series, this volume number and the
    // right medium (VolumeEditionMatcher), and the first one that does wins.
    //
    // Volume.Isbn is never rewritten from here. It records the edition the reader owns; the
    // record that happened to supply a date is not that.
    private async Task<VolumeEnrichment?> ResolveBySearchAsync(
        Series series,
        Volume volume,
        SyncRun run,
        LookupBudget budget,
        CancellationToken cancellationToken)
    {
        if (budget.IsExhausted)
        {
            return null;
        }

        budget.Spend(1);
        var candidates = await candidateSource.FindAsync(
            series.TitleEnglish, volume.Number, cancellationToken);

        foreach (var candidate in candidates.Isbns)
        {
            if (budget.IsExhausted)
            {
                return null;
            }

            // The reader's own ISBN was already asked for and is what got us here.
            if (string.Equals(candidate, volume.Isbn, StringComparison.Ordinal)
                || Isbn.IsJapaneseEdition(candidate))
            {
                continue;
            }

            budget.Spend(1);
            var enrichment = await enrichmentSource.GetByIsbnAsync(candidate, cancellationToken);

            if (enrichment is null
                || enrichment.Language is EditionLanguage.Other
                || !VolumeEditionMatcher.Matches(series.TitleEnglish, volume.Number, series.Format, enrichment))
            {
                continue;
            }

            // The record has to say which medium it is, not merely fail to say it is the
            // other one. This path writes to the shelf with nobody looking, and the case it
            // guards against is real: a discovery run over this shelf proposed three
            // Kodansha manga volumes for a Yen Press light novel, all of them titled just
            // "…Slime 18" with no marker at all. Where Open Library does not say, this does
            // not guess — the volume is left alone and the review file is the way in.
            if (!VolumeEditionMatcher.ConfirmsMedium(series.Format, enrichment))
            {
                logger.LogInformation(
                    "{TitleEnglish} volume {VolumeNumber}: {RecordKey} (\"{RecordTitle}\", {Publisher}) is the "
                    + "right volume but does not state that it is a {Format}; left for review",
                    series.TitleEnglish,
                    volume.Number,
                    enrichment.SourceRecordKey,
                    enrichment.Title,
                    enrichment.Publisher,
                    series.Format);

                continue;
            }

            logger.LogInformation(
                "{TitleEnglish} volume {VolumeNumber}: ISBN {Isbn} is not indexed, but {RecordKey} "
                + "({CandidateIsbn}, \"{RecordTitle}\") is the same volume",
                series.TitleEnglish,
                volume.Number,
                volume.Isbn,
                enrichment.SourceRecordKey,
                candidate,
                enrichment.Title);

            run.VolumesResolvedBySearch++;

            return enrichment;
        }

        return null;
    }
}
