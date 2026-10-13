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
        VolumeFactWriter factWriter,
        ICoverStore? coverStore = null) =>
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
            new BonusVolumeWriter(NullLogger<BonusVolumeWriter>.Instance),
            new WholeExtraVolumeWriter(NullLogger<WholeExtraVolumeWriter>.Instance),
            new NextMainVolumeWriter(NullLogger<NextMainVolumeWriter>.Instance),
            factWriter,
            new DanglingVolumeCoverHealer(coverStore ?? EveryCoverPresent(), NullLogger<DanglingVolumeCoverHealer>.Instance));

    /// <summary>
    /// A store holding every file a key names, so a test that sets a cover key keeps it.
    /// </summary>
    public static ICoverStore EveryCoverPresent()
    {
        var store = Substitute.For<ICoverStore>();
        store.ExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        return store;
    }

    /// <summary>The usual writer: a cover cache that answers, and nothing observed.</summary>
    public static VolumeFactWriter Writer(IVolumeCoverCache? coverCache = null) =>
        new(coverCache ?? Substitute.For<IVolumeCoverCache>(), NullLogger<VolumeFactWriter>.Instance);
}
