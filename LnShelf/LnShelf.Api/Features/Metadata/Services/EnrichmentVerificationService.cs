namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// Proves, against the live sources, that a title, a description and a cover can still be
/// fetched for a series and for its individual volumes. Writes nothing anywhere.
/// </summary>
// The owner's requirement, in his words: "you need to have some sort of system that
// verifies that it actually works". Unit tests cannot answer that — every source here is a
// free community API that can change its payload, rename a field or start refusing an
// anonymous caller without anyone committing a line. So this runs the real seams and reads
// the real answers.
//
// It deliberately simulates the *freshly added* series: an in-memory Series with volumes
// 1..N and no ISBNs at all, which is the case phase 3.10 existed to fix and the one with no
// staging data to lean on. Nothing is loaded from the shelf, nothing is saved to it, and
// the cover cache handed to the writer stores nothing (VerifyingVolumeCoverCache).
//
// Every seam is constructor-injected from the container, so what is checked is what the app
// actually runs. VolumeEnrichmentService and VolumeFactWriter are the two exceptions and
// are built with `new` — only so the verifying cover cache can be substituted for the one
// that writes to MinIO. They are otherwise the same classes the nightly sync uses.
public sealed class EnrichmentVerificationService(
    ISeriesMetadataSource metadataSource,
    IReleaseSourceMatcher releaseSourceMatcher,
    IVolumeCatalogueSource catalogueSource,
    IVolumeEnrichmentSource enrichmentSource,
    IIsbnCandidateSource candidateSource,
    VerificationImageProbe imageProbe,
    TimeProvider timeProvider,
    ILogger<EnrichmentVerificationService> logger,
    ILogger<VolumeEnrichmentService> enrichmentLogger,
    ILogger<VolumeFactWriter> factWriterLogger)
{
    /// <summary>
    /// The three series checked when none are named.
    /// </summary>
    // Chosen because between them they cover every shape this pipeline has to survive: a
    // series AniList publishes no volume count for (Tanya, 86 — so the count has to come
    // from the release source), a title whose punctuation differs between every source
    // ("86―EIGHTY-SIX" against Apple's "86--EIGHTY-SIX"), and the one series on this shelf
    // whose light novel and manga share a name (Slime), which is what the medium bar exists
    // for. These are search phrases rather than titles: they are what actually finds the
    // series, verified live.
    public static readonly IReadOnlyList<string> DefaultSearchTerms =
    [
        "Saga of Tanya the Evil",
        "86 eighty six",
        "that time i got reincarnated as a slime",
    ];

    public const Format DefaultFormat = Format.LightNovel;

    /// <summary>Generous, because a verification run is not competing with anything.</summary>
    public const int LookupAllowance = 100;

    /// <summary>
    /// The bars a series has to clear. Series level is all-or-nothing; volume level is a
    /// share, because a store's catalogue genuinely does miss the odd volume.
    /// </summary>
    // Set against measured reality rather than aspiration. Apple Books answered with a
    // title, a blurb and artwork for 100% of the volumes of all three canaries, and with a
    // date for at least 13 of 14 — the missing one being a future volume whose date the
    // conflict rule withholds on purpose (phase 3.10 observed exactly that on 86 vol 14).
    //
    // So 80% is not "most of them work"; it is a fifth of the shelf silently going missing,
    // which is a regression worth a red exit code. Dates get 70% because they are the one
    // field the conflict rule is allowed to refuse.
    public const int MinimumTitledPercent = 80;
    public const int MinimumDescribedPercent = 80;
    public const int MinimumCoveredPercent = 80;
    public const int MinimumDatedPercent = 70;

    public async Task<EnrichmentVerificationReport> RunAsync(
        IReadOnlyList<string> searchTerms,
        Format format,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(searchTerms);

        logger.LogInformation(
            "Verifying enrichment for {SeriesCount} {Format} series", searchTerms.Count, format);

        var results = new List<SeriesVerification>(searchTerms.Count);

        foreach (var searchTerm in searchTerms)
        {
            results.Add(await VerifyAsync(searchTerm, format, cancellationToken));
        }

        return new EnrichmentVerificationReport { Format = format, Series = results };
    }

    private async Task<SeriesVerification> VerifyAsync(
        string searchTerm,
        Format format,
        CancellationToken cancellationToken)
    {
        var hits = await metadataSource.SearchAsync(searchTerm, format, cancellationToken);
        var hit = hits.FirstOrDefault(candidate => candidate.Format == format);

        if (hit is null)
        {
            return NotResolved(searchTerm, format, $"the search returned no {format}.");
        }

        var metadata = await metadataSource.GetSeriesAsync(hit.ExternalId, cancellationToken);

        if (metadata is null)
        {
            return NotResolved(searchTerm, format, $"the search found {hit.ExternalId}, which has no detail.");
        }

        var seriesLevel = await VerifySeriesLevelAsync(metadata, cancellationToken);
        var series = ToInMemorySeries(metadata);
        var count = await ResolveVolumeCountAsync(series, metadata, cancellationToken);

        AddVolumes(series, count);

        var volumeLevel = count.Volumes is 0
            ? null
            : await EnrichAsync(series, cancellationToken);

        return new SeriesVerification
        {
            SearchTerm = searchTerm,
            Format = format,
            ExternalId = metadata.ExternalId,
            SeriesLevel = seriesLevel,
            VolumeLevel = volumeLevel,
            VolumeCountGround = count.Ground,
            ReleaseSourceNote = count.ReleaseSourceNote,
            Passed = Judge(seriesLevel, volumeLevel),
        };
    }

    /// <summary>
    /// Title, synopsis and cover, straight from the series-level seam — and the cover is
    /// downloaded rather than taken on the source's word.
    /// </summary>
    private async Task<SeriesLevelVerification> VerifySeriesLevelAsync(
        SeriesMetadata metadata,
        CancellationToken cancellationToken)
    {
        VerifiedImage? cover = null;

        if (Uri.TryCreate(metadata.CoverImageUrl, UriKind.Absolute, out var coverUri))
        {
            cover = await imageProbe.ProbeAsync(coverUri, cancellationToken);
        }

        return new SeriesLevelVerification
        {
            Title = metadata.TitleEnglish,
            Description = metadata.Description,
            Cover = cover,
        };
    }

    /// <summary>
    /// The series as the shelf would hold it the moment it was added, and no further.
    /// </summary>
    // Never attached to a DbContext and never saved. The titles matter: the release source
    // is searched on romaji first, and the catalogue is searched on the English title.
    private static Series ToInMemorySeries(SeriesMetadata metadata) => new()
    {
        Id = Guid.CreateVersion7(),
        AniListId = metadata.ExternalId,
        Format = metadata.Format,
        TitleEnglish = metadata.TitleEnglish,
        TitleRomaji = metadata.TitleRomaji,
        TitleNative = metadata.TitleNative,
        PublicationStatus = metadata.PublicationStatus,
    };

    /// <summary>
    /// How many volumes to check, and on whose authority.
    /// </summary>
    // The precedence order is the shelf's own (CLAUDE.md gives volume counts to
    // MangaUpdates), with one addition for the case neither series-level source can answer:
    // the store's own highest confirmed volume number. That ground is weaker and is named
    // as such in the report rather than quietly used.
    private async Task<VolumeCount> ResolveVolumeCountAsync(
        Series series,
        SeriesMetadata metadata,
        CancellationToken cancellationToken)
    {
        var match = await releaseSourceMatcher.MatchAsync(series, new HashSet<long>(), cancellationToken);

        if (match.IsLinked && match.MatchedSeries?.VolumeCount is { } releaseCount)
        {
            return new VolumeCount(releaseCount, $"{releaseCount} from the release source", FromReleaseSource: true);
        }

        // Non-fatal by design: an unlinked series still has volumes, and the point of the
        // run is what the per-volume sources can say about them.
        var note = match.IsLinked
            ? $"linked, but stated no volume count ({match.Explanation})"
            : $"unlinked ({match.Outcome}: {match.Explanation})";

        if (metadata.Volumes.Count > 0)
        {
            return new VolumeCount(
                metadata.Volumes.Count,
                $"{metadata.Volumes.Count} from the metadata source",
                FromReleaseSource: false)
            {
                ReleaseSourceNote = note,
            };
        }

        var catalogueCount = await HighestConfirmedCatalogueNumberAsync(series, cancellationToken);

        return new VolumeCount(
            catalogueCount,
            catalogueCount is 0
                ? "none (no source named a volume count)"
                : $"{catalogueCount} from the store catalogue's highest confirmed volume",
            FromReleaseSource: false)
        {
            ReleaseSourceNote = note,
        };
    }

    /// <summary>
    /// The highest volume number the store catalogue positively confirms for this series.
    /// </summary>
    // The same two filters the enrichment pass applies, for the same reason: a store search
    // answers with spin-offs, the other medium and unrelated series that share a word.
    // Cheap, because the catalogue source caches its answer for a day — the enrichment pass
    // that follows reads the same response rather than asking again.
    private async Task<int> HighestConfirmedCatalogueNumberAsync(
        Series series,
        CancellationToken cancellationToken)
    {
        var catalogue = await catalogueSource.GetVolumesAsync(
            series.TitleEnglish, series.Format, cancellationToken);

        var highest = 0;

        foreach (var record in catalogue)
        {
            if (VolumeEditionMatcher.TryMatchVolumeNumber(
                    series.TitleEnglish, series.Format, record.Title, out var number)
                && VolumeEditionMatcher.ConfirmsMedium(series.Format, record.Title)
                && number > highest)
            {
                highest = number;
            }
        }

        return highest;
    }

    /// <summary>
    /// Volumes 1..N, bare — no ISBN, no title, no date.
    /// </summary>
    // No ISBNs on purpose. That is what a freshly added series looks like, and it means the
    // by-ISBN pass makes no requests at all: what is being verified is the keyless path.
    //
    // IsReleased is set only when the release source supplied the count, because that is
    // precisely what the real refresh does with one. It matters to the result: the writer
    // refuses a future date on a volume the count already calls in print, so marking these
    // released is what makes the report honest about dates rather than flattering.
    private static void AddVolumes(Series series, VolumeCount count)
    {
        for (var number = 1; number <= count.Volumes; number++)
        {
            series.Volumes.Add(new Volume
            {
                Id = Guid.CreateVersion7(),
                Number = number,
                IsReleased = count.FromReleaseSource,
            });
        }
    }

    private async Task<VolumeLevelVerification> EnrichAsync(Series series, CancellationToken cancellationToken)
    {
        var coverCache = new VerifyingVolumeCoverCache(imageProbe);

        var enrichment = new VolumeEnrichmentService(
            catalogueSource,
            enrichmentSource,
            candidateSource,
            new VolumeFactWriter(coverCache, factWriterLogger),
            enrichmentLogger);

        // A run object nobody saves. The writer counts into it and hangs discrepancy rows
        // off it, both of which are wanted behaviour — and both are thrown away with it.
        var run = new SyncRun { Id = Guid.CreateVersion7(), StartedAt = timeProvider.GetUtcNow() };
        var today = DateOnly.FromDateTime(timeProvider.GetLocalNow().Date);

        await enrichment.EnrichAsync(series, run, today, new LookupBudget(LookupAllowance), cancellationToken);

        logger.LogInformation(
            "{TitleEnglish}: enriched {VolumeCount} volume(s), {Failures} failure(s), {Gaps} gap(s)",
            series.TitleEnglish,
            series.Volumes.Count,
            run.EnrichmentFailures,
            run.EnrichmentGaps);

        return Count(series.Volumes, coverCache.Verified);
    }

    /// <summary>
    /// Turns one series' enriched volumes into the numbers the report prints.
    /// </summary>
    // Public and static so the counting can be asserted without a network: it is the half
    // of this class that decides what "covered" means, and that judgement should not only
    // ever be exercised against the live internet.
    public static VolumeLevelVerification Count(
        IEnumerable<Volume> volumes,
        IReadOnlyList<VerifiedImage> covers)
    {
        ArgumentNullException.ThrowIfNull(volumes);
        ArgumentNullException.ThrowIfNull(covers);

        var total = 0;
        var titled = 0;
        var described = 0;
        var covered = 0;
        var dated = 0;

        foreach (var volume in volumes)
        {
            total++;

            if (volume.Title is not null)
            {
                titled++;
            }

            if (volume.Description is not null)
            {
                described++;
            }

            // The key, not a URL: it is set only where the cover cache returned one, and
            // the verifying cache only returns one for bytes that decoded as an image.
            if (volume.CoverObjectKey is not null)
            {
                covered++;
            }

            if (volume.ReleaseDate is not null)
            {
                dated++;
            }
        }

        return new VolumeLevelVerification
        {
            Total = total,
            Titled = titled,
            Described = described,
            Covered = covered,
            Dated = dated,
            CoverByteCount = covers.Sum(cover => cover.ByteCount),
            LargestCover = covers.MaxBy(cover => (long)cover.Width * cover.Height),
        };
    }

    /// <summary>
    /// Whether one series' result clears the bars.
    /// </summary>
    // Public and static for the same reason Count is: the thresholds are the whole point of
    // the command, and a threshold that can only be exercised by running the real internet
    // is a threshold nobody can change safely.
    public static bool Judge(SeriesLevelVerification? seriesLevel, VolumeLevelVerification? volumeLevel) =>
        seriesLevel is { HasTitle: true, HasDescription: true, HasCover: true }
        && volumeLevel is not null
        && Meets(volumeLevel.Titled, volumeLevel.Total, MinimumTitledPercent)
        && Meets(volumeLevel.Described, volumeLevel.Total, MinimumDescribedPercent)
        && Meets(volumeLevel.Covered, volumeLevel.Total, MinimumCoveredPercent)
        && Meets(volumeLevel.Dated, volumeLevel.Total, MinimumDatedPercent);

    /// <summary>Integer arithmetic, so a share exactly on its threshold passes.</summary>
    // A double comparison would work here too, and would leave a reader wondering whether
    // 4 of 5 really is >= 0.8. This one cannot be wondered about.
    private static bool Meets(int count, int total, int minimumPercent) =>
        total > 0 && count * 100 >= total * minimumPercent;

    private static SeriesVerification NotResolved(string searchTerm, Format format, string reason) =>
        new()
        {
            SearchTerm = searchTerm,
            Format = format,
            VolumeCountGround = "none (the series was never resolved)",
            Failure = reason,
            Passed = false,
        };

    /// <summary>How many volumes are being checked, and who said so.</summary>
    private sealed record VolumeCount(int Volumes, string Ground, bool FromReleaseSource)
    {
        /// <summary>Set when the release source did not supply the count. Non-fatal.</summary>
        public string? ReleaseSourceNote { get; init; }
    }
}
