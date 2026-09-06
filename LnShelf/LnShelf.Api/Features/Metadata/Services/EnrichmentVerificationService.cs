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
// actually runs — including the enrichment pipeline itself, which
// VerificationEnrichmentRunner builds from the container with only the cover cache
// substituted.
//
// The series-resolution step goes through ISeriesSearchService rather than straight at
// AniList, which is what the search endpoint does too. That is not tidiness: with AniList's
// API disabled, a gate that could only resolve a series through AniList reported FAIL 0/3
// on a shelf that was working — the fetching it exists to police had simply moved to
// another source. The report names which source answered, so a pass through the fallback
// cannot be mistaken for a normal one.
public sealed class EnrichmentVerificationService(
    ISeriesSearchService searchService,
    SearchHitResolver hitResolver,
    VerificationVolumeCountResolver volumeCountResolver,
    VerificationEnrichmentRunner enrichmentRunner,
    VerificationImageProbe imageProbe,
    ILogger<EnrichmentVerificationService> logger)
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
        var outcome = await searchService.SearchAsync(searchTerm, format, cancellationToken);
        var hit = outcome.Results.FirstOrDefault(candidate => candidate.Format == format);

        if (hit is null)
        {
            return NotResolved(searchTerm, format, $"the search returned no {format}.", outcome.Source);
        }

        var resolved = await hitResolver.ResolveAsync(hit, cancellationToken);

        if (resolved is null)
        {
            return NotResolved(
                searchTerm,
                format,
                $"the search found {hit.SourceId}, which has no detail.",
                outcome.Source);
        }

        var seriesLevel = await VerifySeriesLevelAsync(resolved, cancellationToken);
        var series = ToInMemorySeries(resolved);
        var count = await volumeCountResolver.ResolveAsync(series, resolved, cancellationToken);

        AddVolumes(series, count);

        var volumeLevel = count.Volumes is 0
            ? null
            : await enrichmentRunner.RunAsync(series, cancellationToken);

        return new SeriesVerification
        {
            SearchTerm = searchTerm,
            Format = format,
            Source = resolved.Source,
            ExternalId = resolved.SourceId,
            SeriesLevel = seriesLevel,
            VolumeLevel = volumeLevel,
            VolumeCountGround = count.Ground,
            ReleaseSourceNote = count.ReleaseSourceNote,

            // Read off the series the enrichment just wrote it onto, so the report says which
            // of the titles the store actually accepted — or, when it stays null, that none
            // of them was. On the MangaUpdates path that is the difference between a pass and
            // a diagnosis.
            CatalogueTitle = series.CatalogueTitle,
            KnownTitleCount = 1 + series.AlternativeTitles.Length,

            Passed = VerificationVerdict.Judge(seriesLevel, volumeLevel),
        };
    }

    /// <summary>
    /// Title, synopsis and cover, straight from the series-level seam — and the cover is
    /// downloaded rather than taken on the source's word.
    /// </summary>
    // Works on the resolved shape rather than on AniList's, so the same three checks run
    // against whichever source answered — a MangaUpdates record has a title, a synopsis and
    // a cover URL of its own, and all three are worth exactly the same scrutiny.
    private async Task<SeriesLevelVerification> VerifySeriesLevelAsync(
        ResolvedSeries resolved,
        CancellationToken cancellationToken)
    {
        VerifiedImage? cover = null;

        if (Uri.TryCreate(resolved.CoverImageUrl, UriKind.Absolute, out var coverUri))
        {
            cover = await imageProbe.ProbeAsync(coverUri, cancellationToken);
        }

        return new SeriesLevelVerification
        {
            Title = resolved.TitleEnglish,
            Description = resolved.Description,
            Cover = cover,
        };
    }

    /// <summary>
    /// The series as the shelf would hold it the moment it was added, and no further.
    /// </summary>
    // Never attached to a DbContext and never saved. The titles matter: the release source
    // is searched on romaji first, and the catalogue arbitrates across every title the
    // series is known by — which is why the alternatives are copied over rather than left
    // empty. A gate that handed the enrichment one romaji title would be checking a
    // narrower pipeline than the one the add flow runs, and would fail on a working shelf.
    //
    // The external id is written to the column the resolving source owns, which is also what
    // makes the count step honest: a series resolved through MangaUpdates arrives already
    // linked, and nothing then goes matching for a link it has.
    //
    // CatalogueTitle is deliberately left null: this series has never been enriched before,
    // so the run measures the arbitration from cold, exactly as the night a series is added.
    private static Series ToInMemorySeries(ResolvedSeries resolved) => new()
    {
        Id = Guid.CreateVersion7(),
        AniListId = resolved.Source is MetadataSourceName.AniList ? (int)resolved.SourceId : null,
        MangaUpdatesId = resolved.Source is MetadataSourceName.MangaUpdates ? resolved.SourceId : null,
        Format = resolved.Format,
        TitleEnglish = resolved.TitleEnglish,
        TitleRomaji = resolved.TitleRomaji,
        TitleNative = resolved.TitleNative,
        AlternativeTitles = [.. resolved.AlternativeTitles],
        PublicationStatus = resolved.PublicationStatus,
    };

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
    private static void AddVolumes(Series series, VerificationVolumeCount count)
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

    /// <param name="source">
    /// Which source was asked. Reported even on a failure — "MangaUpdates has never heard
    /// of it" and "AniList has never heard of it" are different facts.
    /// </param>
    private static SeriesVerification NotResolved(
        string searchTerm,
        Format format,
        string reason,
        MetadataSourceName source) =>
        new()
        {
            SearchTerm = searchTerm,
            Format = format,
            Source = source,
            VolumeCountGround = "none (the series was never resolved)",
            Failure = reason,
            Passed = false,
        };
}
