using static LnShelf.Api.Tests.Features.Metadata.Services.VolumeEnrichmentTestContext;

namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// Bonus volumes the store lists between the main ones — the one kind of volume the
/// catalogue may create.
/// </summary>
// Record titles are Apple Books' own for Torture Princess, captured 2026-10-11: volumes 1–9
// and "Vol. 7.5", the short-story collection published between 7 and 8.
public sealed class VolumeEnrichmentBonusVolumeTests
{
    private const string Title = "Torture Princess: Fremd Torturchen";

    private static readonly DateOnly Today = new(2026, 10, 11);

    [Fact]
    public async Task EnrichAsync_TheStoreListsABonusVolume_CreatesItAndWritesItsFacts()
    {
        var series = Series(Enumerable.Range(1, 9).Select(number => Volume(number)).ToArray());
        series.ReleaseSourceVolumeCount = 9;
        var run = Run();

        await CreateService(Catalogue(
                Record("Vol. 7 (light novel)", new DateOnly(2021, 4, 27), "real"),
                Record("Vol. 7.5 (light novel)", new DateOnly(2022, 1, 11), "bonus")))
            .EnrichAsync(series, run, Today, Budget(10), expectedBonusVolumes: 1, CancellationToken.None);

        var bonus = series.Volumes.Single(volume => volume.Number == 7.5m);
        bonus.ReleaseDate.ShouldBe(new DateOnly(2022, 1, 11));
        bonus.Description.ShouldBe("bonus");
        bonus.IsLocalOnly.ShouldBeFalse();

        // In print by the release source's count, set at creation so a later sweep does not
        // flip it and raise the new-volume badge for a book out since 2022.
        bonus.IsReleased.ShouldBeTrue();
        run.NewVolumes.ShouldBe(1);

        // And volume 7 keeps its own record rather than the bonus volume's.
        series.Volumes.Single(volume => volume.Number == 7).Description.ShouldBe("real");
    }

    [Fact]
    public async Task EnrichAsync_EveryVolumeEnrichedButABonusVolumeMissing_StillAsksTheStore()
    {
        var series = Series(Enumerable.Range(1, 9).Select(number => Enriched(Volume(number))).ToArray());

        await CreateService(Catalogue(
                Record("Vol. 7 (light novel)", new DateOnly(2021, 4, 27), "real"),
                Record("Vol. 7.5 (light novel)", new DateOnly(2022, 1, 11), "bonus")))
            .EnrichAsync(series, Run(), Today, Budget(10), expectedBonusVolumes: 1, CancellationToken.None);

        series.Volumes.ShouldContain(volume => volume.Number == 7.5m);
    }

    [Fact]
    public async Task EnrichAsync_EveryVolumeEnrichedAndNoBonusExpected_DoesNotAskTheStore()
    {
        var series = Series(Enumerable.Range(1, 9).Select(number => Enriched(Volume(number))).ToArray());
        var catalogue = Catalogue(Record("Vol. 7.5 (light novel)", new DateOnly(2022, 1, 11), "bonus"));

        await CreateService(catalogue)
            .EnrichAsync(series, Run(), Today, Budget(10), expectedBonusVolumes: 0, CancellationToken.None);

        series.Volumes.ShouldNotContain(volume => volume.Number == 7.5m);
        await catalogue.DidNotReceiveWithAnyArgs().GetVolumesAsync(default!, default, default);
    }

    [Fact]
    public async Task EnrichAsync_ABonusVolumeBeyondTheTrackedVolumes_IsNotCreated()
    {
        // The release source owns how many main volumes there are; a store listing "Vol. 9.5"
        // of a series tracked to 8 is a future the count has not reached.
        var series = Series(Enumerable.Range(1, 8).Select(number => Volume(number)).ToArray());

        await CreateService(Catalogue(
                Record("Vol. 8 (light novel)", new DateOnly(2022, 5, 3), "real"),
                Record("Vol. 9.5 (light novel)", new DateOnly(2023, 1, 1), "beyond")))
            .EnrichAsync(series, Run(), Today, Budget(10), expectedBonusVolumes: 1, CancellationToken.None);

        series.Volumes.ShouldNotContain(volume => volume.Number == 9.5m);
    }

    [Fact]
    public async Task EnrichAsync_TheMangaOfTheSameBonusVolume_IsNotCreated()
    {
        var series = Series(Enumerable.Range(1, 9).Select(number => Volume(number)).ToArray());

        await CreateService(Catalogue(
                Record("Vol. 7 (light novel)", new DateOnly(2021, 4, 27), "real"),
                Record("Vol. 7.5 (manga)", new DateOnly(2022, 1, 11), "manga")))
            .EnrichAsync(series, Run(), Today, Budget(10), expectedBonusVolumes: 1, CancellationToken.None);

        series.Volumes.ShouldNotContain(volume => volume.Number == 7.5m);
    }

    [Fact]
    public async Task EnrichAsync_TheReleaseSourceCountsNoBonusVolumes_CreatesNone()
    {
        // The store is trusted with where a bonus volume sits, not with whether the edition
        // has one: without "+ 1 Extra" from the release source, a "Vol. 7.5" listing is left be.
        var series = Series(Enumerable.Range(1, 9).Select(number => Volume(number)).ToArray());

        await CreateService(Catalogue(
                Record("Vol. 7 (light novel)", new DateOnly(2021, 4, 27), "real"),
                Record("Vol. 7.5 (light novel)", new DateOnly(2022, 1, 11), "bonus")))
            .EnrichAsync(series, Run(), Today, Budget(10), expectedBonusVolumes: 0, CancellationToken.None);

        series.Volumes.ShouldNotContain(volume => volume.Number == 7.5m);
    }

    private static Volume Enriched(Volume volume)
    {
        volume.ReleaseDate = new DateOnly(2020, 1, 1);
        volume.CoverObjectKey = "volume/applebooks/known.jpg";
        volume.Title = string.Empty;
        volume.Description = "Already enriched.";

        return volume;
    }

    private static VolumeEnrichmentService CreateService(IVolumeCatalogueSource catalogueSource) =>
        VolumeEnrichmentPipeline.Create(
            catalogueSource,
            NoEnrichment(),
            NoCandidates(),
            VolumeEnrichmentPipeline.Writer(CoverCache()));

    private static CatalogueVolume Record(string volumePart, DateOnly releaseDate, string description) => new()
    {
        SourceRecordKey = description,
        Title = $"{Title}, {volumePart}",
        Description = description,
        ReleaseDate = releaseDate,
        ArtworkUrl = new Uri($"https://is1-ssl.mzstatic.com/image/thumb/{description}.jpg/1000x1500bb.jpg"),
    };

    private static SyncRun Run() => VolumeEnrichmentTestContext.Run(Today);

    private static Series Series(params Volume[] volumes) =>
        VolumeEnrichmentTestContext.Series(Title, volumes, aniListId: 97100);
}
