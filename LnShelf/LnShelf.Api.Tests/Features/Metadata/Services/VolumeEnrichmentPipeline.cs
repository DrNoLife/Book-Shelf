namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// The enrichment pipeline, wired from its seams the way the container wires it.
/// </summary>
// Five test files build this service, and the composition is real code with real
// collaborators rather than one constructor call — so it is written once here. Tests that
// want to observe a collaborator (a cover cache, a catalogue source) still pass their own
// double in; nothing here substitutes anything.
internal static class VolumeEnrichmentPipeline
{
    public static VolumeEnrichmentService Create(
        IVolumeCatalogueSource catalogueSource,
        IVolumeEnrichmentSource enrichmentSource,
        IIsbnCandidateSource candidateSource,
        VolumeFactWriter factWriter) =>
        new(
            catalogueSource,
            new CatalogueTitleArbiter(
                catalogueSource,
                new CatalogueVolumeMatcher(NullLogger<CatalogueVolumeMatcher>.Instance),
                NullLogger<CatalogueTitleArbiter>.Instance),
            new IsbnVolumeEnricher(
                enrichmentSource,
                candidateSource,
                new IsbnCandidateVerifier(enrichmentSource),
                factWriter,
                NullLogger<IsbnVolumeEnricher>.Instance),
            factWriter);

    /// <summary>The usual writer: a cover cache that answers, and nothing observed.</summary>
    public static VolumeFactWriter Writer(IVolumeCoverCache? coverCache = null) =>
        new(coverCache ?? Substitute.For<IVolumeCoverCache>(), NullLogger<VolumeFactWriter>.Instance);
}
