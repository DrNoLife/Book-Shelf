using static LnShelf.Api.Tests.Features.Metadata.Services.VolumeEnrichmentTestContext;

namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// The store and the by-ISBN lookups each spend their own allowance, so one running out never
/// stops the other.
/// </summary>
// The backlog §7 scenario: with one shared allowance, a series with no remembered store title
// could spend its title attempts against an on-demand allowance of eight before a single ISBN
// was looked up.
public sealed class VolumeEnrichmentBudgetTests
{
    private static readonly DateOnly Today = new(2026, 10, 13);

    private static readonly string[] AlternativeTitles =
    [
        "Youjo Senki",
        "Saga of Tanya",
        "Tanya the Evil",
        "Military Chronicle of a Little Girl",
    ];

    [Fact]
    public async Task EnrichAsync_TitleAttemptsTheStoreDoesNotRecognise_DoNotSpendTheIsbnAllowance()
    {
        var series = SeriesWithIsbns(volumeCount: 8);
        var catalogueSource = NoCatalogue();
        var enrichmentSource = EnrichmentSource(Edition());
        var budget = new EnrichmentBudget(catalogueRequests: 10, isbnLookups: 8);

        await CreateService(catalogueSource, enrichmentSource)
            .EnrichAsync(series, Run(Today), Today, budget, expectedBonusVolumes: 0, CancellationToken.None);

        // Five titles tried at the store (the shelf's own and four alternatives), and every
        // one of the eight volumes still looked up by ISBN.
        (10 - budget.Catalogue.Remaining).ShouldBe(5);
        await enrichmentSource.Received(8).GetByIsbnAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        budget.ByIsbn.IsExhausted.ShouldBeTrue();
    }

    [Fact]
    public async Task EnrichAsync_AnExhaustedCatalogueAllowance_StillLooksUpIsbns()
    {
        var series = SeriesWithIsbns(volumeCount: 8);
        var catalogueSource = NoCatalogue();
        var enrichmentSource = EnrichmentSource(Edition());

        await CreateService(catalogueSource, enrichmentSource).EnrichAsync(
            series, Run(Today), Today, new EnrichmentBudget(0, 8), expectedBonusVolumes: 0, CancellationToken.None);

        await catalogueSource.DidNotReceive()
            .GetVolumesAsync(Arg.Any<string>(), Arg.Any<Format>(), Arg.Any<CancellationToken>());
        await enrichmentSource.Received(8).GetByIsbnAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnrichAsync_AnExhaustedIsbnAllowance_StillAsksTheStore()
    {
        var series = SeriesWithIsbns(volumeCount: 8);
        var catalogueSource = NoCatalogue();
        var enrichmentSource = EnrichmentSource(Edition());

        await CreateService(catalogueSource, enrichmentSource).EnrichAsync(
            series, Run(Today), Today, new EnrichmentBudget(5, 0), expectedBonusVolumes: 0, CancellationToken.None);

        await catalogueSource.Received()
            .GetVolumesAsync(Arg.Any<string>(), Arg.Any<Format>(), Arg.Any<CancellationToken>());
        await enrichmentSource.DidNotReceive().GetByIsbnAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static VolumeEnrichmentService CreateService(
        IVolumeCatalogueSource catalogueSource,
        IVolumeEnrichmentSource enrichmentSource) =>
        VolumeEnrichmentPipeline.Create(
            catalogueSource,
            enrichmentSource,
            NoCandidates(),
            VolumeEnrichmentPipeline.Writer(CoverCache()));

    /// <summary>
    /// Eight volumes with distinct, English-market ISBNs and nothing else known about them.
    /// </summary>
    private static Series SeriesWithIsbns(int volumeCount) => Series(
        "The Saga of Tanya the Evil",
        [.. Enumerable.Range(1, volumeCount).Select(number => Volume(number, isbn: $"978031636{number:D4}"))],
        alternativeTitles: AlternativeTitles);

    /// <summary>An answer for any ISBN, so each volume costs exactly one lookup.</summary>
    private static VolumeEnrichment Edition() => new()
    {
        SourceRecordKey = "/books/OL26842920M",
        Language = EditionLanguage.English,
    };
}
