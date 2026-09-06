namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// The second enrichment pass: one lookup per volume whose ISBN is known, and a name search
/// for the ones whose ISBN the source has never indexed.
/// </summary>
// Unambiguous but narrow — an ISBN lookup answers about exactly one edition, and only the
// 59% of volumes whose ISBN is known can be asked about at all. It runs after the catalogue
// pass and fills what that pass left (VolumeEnrichmentService documents the order).
public sealed class IsbnVolumeEnricher(
    IVolumeEnrichmentSource enrichmentSource,
    IIsbnCandidateSource candidateSource,
    IsbnCandidateVerifier candidateVerifier,
    VolumeFactWriter factWriter,
    ILogger<IsbnVolumeEnricher> logger)
{
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

        foreach (var volume in series.Volumes.OrderBy(volume => volume.Number))
        {
            if (budget.IsExhausted)
            {
                break;
            }

            if (!VolumeGaps.WorthAskingByIsbn(volume))
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
            // The ISBN check in VolumeGaps catches the Japanese-market ones; this catches
            // the rest — a Brazilian or French edition sharing a work with the English one.
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
            VolumeFactsMapper.ToFacts(enrichment, enrichmentSource.SourceName),
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
    // right medium (IsbnCandidateVerifier), and the first one that does wins.
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

            // Judged from the ISBN itself, which costs no request: the reader's own was
            // already asked for and is what got us here, and a Japanese registration group
            // means a Japanese-market edition.
            if (!IsbnCandidateVerifier.IsWorthAsking(candidate, volume.Isbn))
            {
                continue;
            }

            budget.Spend(1);

            var verified = await candidateVerifier.VerifyAsync(
                series.TitleEnglish, volume.Number, series.Format, candidate, cancellationToken);

            if (verified is null)
            {
                continue;
            }

            // The record has to say which medium it is, not merely fail to say it is the
            // other one. This path writes to the shelf with nobody looking, and the case it
            // guards against is real: a discovery run over this shelf proposed three
            // Kodansha manga volumes for a Yen Press light novel, all of them titled just
            // "…Slime 18" with no marker at all. Where Open Library does not say, this does
            // not guess — the volume is left alone and the review file is the way in.
            if (!verified.ConfirmsMedium)
            {
                logger.LogInformation(
                    "{TitleEnglish} volume {VolumeNumber}: {RecordKey} (\"{RecordTitle}\", {Publisher}) is the "
                    + "right volume but does not state that it is a {Format}; left for review",
                    series.TitleEnglish,
                    volume.Number,
                    verified.Record.SourceRecordKey,
                    verified.Record.Title,
                    verified.Record.Publisher,
                    series.Format);

                continue;
            }

            logger.LogInformation(
                "{TitleEnglish} volume {VolumeNumber}: ISBN {Isbn} is not indexed, but {RecordKey} "
                + "({CandidateIsbn}, \"{RecordTitle}\") is the same volume",
                series.TitleEnglish,
                volume.Number,
                volume.Isbn,
                verified.Record.SourceRecordKey,
                candidate,
                verified.Record.Title);

            run.VolumesResolvedBySearch++;

            return verified.Record;
        }

        return null;
    }
}
