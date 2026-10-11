namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// Builds an enrichment pipeline whose cover writes go somewhere other than the cover store.
/// </summary>
// A delegate rather than an interface, and registered in MetadataFeatureExtensions where
// the rest of the wiring lives: the only thing that ever varies is the cover cache, and the
// only caller is the verification run.
public delegate VolumeEnrichmentService VolumeEnrichmentFactory(IVolumeCoverCache coverCache);

/// <summary>
/// Runs the real enrichment over an in-memory series and counts what came back.
/// </summary>
// The one place the verification command departs from "resolve exactly what the app
// resolves": the cover cache is swapped for one that downloads and measures the bytes
// instead of storing them (VerifyingVolumeCoverCache). Everything else in the pipeline —
// the arbiter, the matcher, the writer and its conflict rules — is built from the container
// by the factory, so what is being verified is what the nightly sync actually runs.
public sealed class VerificationEnrichmentRunner(
    VolumeEnrichmentFactory enrichmentFactory,
    VerificationImageProbe imageProbe,
    TimeProvider timeProvider,
    ILogger<VerificationEnrichmentRunner> logger)
{
    /// <summary>Generous, because a verification run is not competing with anything.</summary>
    public const int LookupAllowance = 100;

    public async Task<VolumeLevelVerification> RunAsync(Series series, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(series);

        var coverCache = new VerifyingVolumeCoverCache(imageProbe);
        var enrichment = enrichmentFactory(coverCache);

        // A run object nobody saves. The writer counts into it and hangs discrepancy rows
        // off it, both of which are wanted behaviour — and both are thrown away with it.
        var run = new SyncRun { Id = Guid.CreateVersion7(), StartedAt = timeProvider.GetUtcNow() };
        var today = timeProvider.Today();

        // No bonus volumes are created: the verification report checks the volumes a count
        // produced, and a bonus volume is not one of them.
        await enrichment.EnrichAsync(
            series, run, today, new LookupBudget(LookupAllowance), expectedBonusVolumes: 0, cancellationToken);

        logger.LogInformation(
            "{TitleEnglish}: enriched {VolumeCount} volume(s), {Failures} failure(s), {Gaps} gap(s)",
            series.TitleEnglish,
            series.Volumes.Count,
            run.EnrichmentFailures,
            run.EnrichmentGaps);

        return VerificationVerdict.Count(series.Volumes, coverCache.Verified);
    }
}
