namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// Proposes ISBNs for owned volumes that have none, by searching Open Library and
/// verifying every candidate one edition at a time. Writes nothing to the shelf.
/// </summary>
// The measured recipe, and the only one that works for a volume whose ISBN is unknown
// (.research/volume-enrichment/e-isbn-first-pipeline.md): search the phrase, take the
// union of every hit's ISBN list, ignore the hit titles entirely because they will be
// Japanese, then verify each candidate ISBN with a direct edition lookup and keep the
// one that says English.
//
// Its measured success rate was 0 of 7 clean resolutions, so the output is a file for
// a person to read rather than a write — a separate command applies whatever survives
// review. Failures are data gaps, not bugs: they stay null and the volume keeps
// rendering exactly as it does today.
public sealed class IsbnDiscoveryService(
    LnShelfDbContext dbContext,
    IIsbnCandidateSource candidateSource,
    IsbnCandidateVerifier candidateVerifier,
    ILogger<IsbnDiscoveryService> logger)
{
    /// <summary>
    /// One row per owned volume with no ISBN, whether or not anything was found.
    /// </summary>
    // Every volume gets a row, including the ones that found nothing: a review file
    // that silently omits its failures reads as "these are all of them".
    public async Task<IReadOnlyList<IsbnDiscoveryRow>> RunAsync(CancellationToken cancellationToken)
    {
        var targets = await LoadTargetsAsync(cancellationToken);

        logger.LogInformation(
            "ISBN discovery over {VolumeCount} owned volume(s) with no ISBN", targets.Count);

        var rows = new List<IsbnDiscoveryRow>(targets.Count);

        foreach (var target in targets)
        {
            // Per-volume failure isolation, the same rule the sync run follows: one
            // volume's lookup dying must not cost the other forty-nine theirs.
            try
            {
                rows.Add(await DiscoverAsync(target, cancellationToken));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "ISBN discovery failed for {SeriesTitle} volume {VolumeNumber}; continuing",
                    target.SeriesTitle,
                    target.VolumeNumber);

                rows.Add(target.ToRow(IsbnDiscoveryOutcome.LookupFailed, exception.Message));
            }
        }

        logger.LogInformation(
            "ISBN discovery proposed {ProposedCount} ISBN(s) for {VolumeCount} volume(s)",
            rows.Count(row => row.Isbn is not null),
            rows.Count);

        return rows;
    }

    /// <summary>
    /// Owned volumes with no ISBN — the ones the shelf can say something about but
    /// cannot look up.
    /// </summary>
    // Owned only, deliberately. Of the not-yet-owned volumes, 6 of 7 probed returned
    // no search results at all: Open Library catalogues the past, and asking it about
    // volume 17 spends requests to learn nothing.
    private async Task<List<DiscoveryTarget>> LoadTargetsAsync(CancellationToken cancellationToken)
    {
        // One query, projected to the two facts the search phrase needs. Flattened in
        // memory afterwards because the whole result is a few dozen rows and ordering
        // a nested collection is not worth making SQL do.
        var series = await dbContext.Series
            .AsNoTracking()
            .Select(candidate => new
            {
                candidate.Id,
                candidate.TitleEnglish,
                candidate.Format,
                VolumeNumbers = candidate.Volumes
                    .Where(volume => volume.Isbn == null
                                     && volume.UserState != null
                                     && volume.UserState.Owned)
                    .Select(volume => volume.Number)
                    .ToList(),
            })
            .ToListAsync(cancellationToken);

        return
        [
            .. series
                .OrderBy(candidate => candidate.TitleEnglish, StringComparer.OrdinalIgnoreCase)
                .SelectMany(candidate => candidate.VolumeNumbers
                    .Order()
                    .Select(number => new DiscoveryTarget
                    {
                        SeriesId = candidate.Id,
                        SeriesTitle = candidate.TitleEnglish,
                        Format = candidate.Format,
                        VolumeNumber = number,
                    })),
        ];
    }

    private async Task<IsbnDiscoveryRow> DiscoverAsync(
        DiscoveryTarget target,
        CancellationToken cancellationToken)
    {
        var candidates = await candidateSource.FindAsync(
            target.SeriesTitle, target.VolumeNumber, cancellationToken);

        if (candidates.WorksFound is 0)
        {
            // The common answer for a recent volume: Open Library catalogues the past,
            // and it has not caught up with this one yet.
            return target.ToRow(
                IsbnDiscoveryOutcome.NoSearchResults,
                $"{candidateSource.SourceName} has no work matching that phrase.");
        }

        if (candidates.Isbns.Count is 0)
        {
            return target.ToRow(
                IsbnDiscoveryOutcome.NoCandidateIsbns,
                $"{candidates.WorksFound} work(s) matched, none of them listing an ISBN.");
        }

        VolumeEnrichment? likely = null;
        string? likelyIsbn = null;

        VolumeEnrichment? unconfirmed = null;
        string? unconfirmedIsbn = null;

        foreach (var candidate in candidates.Isbns)
        {
            // Every candidate goes through the same verifier the nightly search fallback
            // uses: not indexed, another language, a Japanese registration group or the
            // wrong volume all come back as nothing to say.
            var verified = await candidateVerifier.VerifyAsync(
                target.SeriesTitle, target.VolumeNumber, target.Format, candidate, cancellationToken);

            if (verified is null)
            {
                continue;
            }

            // A record that names English *and* names this medium is the answer. Take it and
            // stop asking — an explicit statement beats every inference below it.
            if (verified is { NamesEnglish: true, ConfirmsMedium: true })
            {
                return target.ToRow(
                    IsbnDiscoveryOutcome.ConfirmedEnglish,
                    $"{candidates.Isbns.Count} candidate(s) offered; this record names English "
                    + $"and says it is a {target.Format}.",
                    verified.Isbn,
                    verified.Record);
            }

            // English, the right volume, but silent about the medium. Kept as a fallback and
            // labelled, because this is where a manga volume gets proposed for a light novel.
            if (verified.NamesEnglish)
            {
                unconfirmed ??= verified.Record;
                unconfirmedIsbn ??= verified.Isbn;
                continue;
            }

            // Unstated: usable only on the publisher's word. Kept as the answer of last
            // resort while the search carries on looking for a record that actually says
            // English.
            if (likelyIsbn is null && EnglishPublishers.Includes(verified.Record.Publisher))
            {
                likely = verified.Record;
                likelyIsbn = verified.Isbn;
            }
        }

        // English and the right volume beats a guess from the publisher, so it is offered
        // first — with the medium called out as the thing to check.
        if (unconfirmedIsbn is not null)
        {
            return target.ToRow(
                IsbnDiscoveryOutcome.MediumUnconfirmed,
                $"{candidates.Isbns.Count} candidate(s) verified; English and the right volume, but the "
                + $"record never says it is a {target.Format} — {unconfirmed!.Publisher} could be the "
                + "other medium of the same story. Check before applying.",
                unconfirmedIsbn,
                unconfirmed);
        }

        if (likelyIsbn is null)
        {
            return target.ToRow(
                IsbnDiscoveryOutcome.NoEnglishEdition,
                $"{candidates.Isbns.Count} candidate(s) verified, not one of them an English edition.");
        }

        return target.ToRow(
            IsbnDiscoveryOutcome.LikelyEnglishPublisher,
            $"{candidates.Isbns.Count} candidate(s) verified; this one states no language, but "
            + $"{likely!.Publisher} is an English-market imprint — check the title.",
            likelyIsbn,
            likely);
    }

    /// <summary>A volume worth asking about, flattened out of the query.</summary>
    private sealed record DiscoveryTarget
    {
        public required Guid SeriesId { get; init; }
        public required string SeriesTitle { get; init; }

        /// <summary>Which medium, so a manga edition cannot be proposed for a light novel.</summary>
        public required Format Format { get; init; }

        public required int VolumeNumber { get; init; }

        public IsbnDiscoveryRow ToRow(
            IsbnDiscoveryOutcome outcome,
            string note,
            string? isbn = null,
            VolumeEnrichment? enrichment = null) => new()
            {
                SeriesId = SeriesId,
                SeriesTitle = SeriesTitle,
                VolumeNumber = VolumeNumber,
                Isbn = isbn,
                OpenLibraryTitle = enrichment?.Title,
                Publisher = enrichment?.Publisher,
                Outcome = outcome,
                Note = note,
            };
    }
}
