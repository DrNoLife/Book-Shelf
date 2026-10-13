using static LnShelf.Api.Tests.Features.Metadata.Services.VolumeEnrichmentTestContext;

namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// Whole volumes past the main count that the release source counts as extras — the second
/// kind of volume the catalogue may create, beside the bonus volumes between the main ones.
/// </summary>
// Record titles are Apple Books' own for Solo Leveling, captured 2026-10-11: IZE Press sells
// Vols. 1 to 15 while its MangaUpdates note says "13+2 Volumes".
public sealed class VolumeEnrichmentWholeExtraVolumeTests
{
    private const string Title = "Solo Leveling";

    private static readonly DateOnly Today = new(2026, 10, 11);
    private static readonly DateOnly Volume14Date = new(2025, 12, 30);
    private static readonly DateOnly Volume15Date = new(2026, 3, 17);

    [Fact]
    public async Task EnrichAsync_TheStoreListsTheExtras_CreatesThemWithTheirDates()
    {
        var series = Series(13);
        var run = Run();

        await CreateService(Catalogue(
                Record(13, new DateOnly(2025, 8, 19)),
                Record(14, Volume14Date),
                Record(15, Volume15Date)))
            .EnrichAsync(series, run, Today, Budget(10), expectedBonusVolumes: 2, CancellationToken.None);

        series.Volumes.Select(volume => volume.Number).Order().ShouldBe(Enumerable.Range(1, 15).Select(number => (decimal)number));
        run.NewVolumes.ShouldBe(2);

        var volume14 = series.Volumes.Single(volume => volume.Number == 14);
        volume14.ReleaseDate.ShouldBe(Volume14Date);
        volume14.ReleaseDateSource.ShouldBe(MetadataSourceName.AppleBooks);
        volume14.CoverObjectKey.ShouldNotBeNull();
        volume14.IsLocalOnly.ShouldBeFalse();

        // The count says 13 are in print, and nothing more: 14 is out by its date, which the
        // projections read, not by a flag the count never granted.
        volume14.IsReleased.ShouldBeFalse();
        series.Volumes.Single(volume => volume.Number == 15).ReleaseDate.ShouldBe(Volume15Date);
    }

    [Fact]
    public async Task EnrichAsync_AGapInTheStoresListing_StopsAtTheGap()
    {
        // Contiguous from the main count, or not at all: a store listing 15 without 14 is not
        // grounds to guess where 14 went.
        var series = Series(13);

        await CreateService(Catalogue(Record(13, new DateOnly(2025, 8, 19)), Record(15, Volume15Date)))
            .EnrichAsync(series, Run(), Today, Budget(10), expectedBonusVolumes: 2, CancellationToken.None);

        series.Volumes.Count.ShouldBe(13);
    }

    [Fact]
    public async Task EnrichAsync_AListingWithNoDate_IsNotCreated()
    {
        var series = Series(13);

        await CreateService(Catalogue(Record(13, new DateOnly(2025, 8, 19)), Record(14, releaseDate: null)))
            .EnrichAsync(series, Run(), Today, Budget(10), expectedBonusVolumes: 2, CancellationToken.None);

        series.Volumes.Count.ShouldBe(13);
    }

    [Fact]
    public async Task EnrichAsync_MoreListedThanTheExtrasCounted_CreatesOnlyAsManyAsCounted()
    {
        var series = Series(13);

        // A listing past the announcement horizon is neither an extra nor a next volume,
        // which isolates the extras rule.
        await CreateService(Catalogue(
                Record(13, new DateOnly(2025, 8, 19)),
                Record(14, Volume14Date),
                Record(15, Today.AddMonths(13))))
            .EnrichAsync(series, Run(), Today, Budget(10), expectedBonusVolumes: 1, CancellationToken.None);

        series.Volumes.Max(volume => volume.Number).ShouldBe(14);
    }

    [Fact]
    public async Task EnrichAsync_ABonusVolumeAlreadyTakesTheExtrasPlace_CreatesNoWholeVolume()
    {
        // Torture Princess' shape: "9 Volumes + 1 Extra", and the extra is Vol. 7.5. A store
        // listing a "Vol. 10" as well does not make the edition eleven books long.
        var series = Series(9);
        series.ReleaseSourceVolumeCount = 9;
        series.Volumes.Add(new Volume { Id = Guid.CreateVersion7(), Number = 7.5m, IsReleased = true });

        // A listing past the announcement horizon is neither an extra nor a next volume,
        // which isolates the extras rule.
        await CreateService(Catalogue(Record(9, new DateOnly(2024, 1, 1)), Record(10, Today.AddMonths(13))))
            .EnrichAsync(series, Run(), Today, Budget(10), expectedBonusVolumes: 1, CancellationToken.None);

        series.Volumes.ShouldNotContain(volume => volume.Number == 10);
    }

    [Fact]
    public async Task EnrichAsync_TheOtherMediumsVolume_IsNotCreated()
    {
        var series = Series(13);

        await CreateService(Catalogue(
                Record(13, new DateOnly(2025, 8, 19)),
                Record(14, Volume14Date, medium: "novel")))
            .EnrichAsync(series, Run(), Today, Budget(10), expectedBonusVolumes: 2, CancellationToken.None);

        series.Volumes.Count.ShouldBe(13);
    }

    [Fact]
    public async Task EnrichAsync_UnderAVolumeCountChoice_CreatesNothing()
    {
        // The user settled the count by hand; the store does not reopen it.
        var series = Series(13);
        series.SourceChoices.Add(new SourceChoice
        {
            Id = Guid.CreateVersion7(),
            SeriesId = series.Id,
            Field = SyncDiscrepancyField.VolumeCount,
            Source = MetadataSourceName.MangaUpdates,
            ChosenAt = DateTimeOffset.UnixEpoch,
        });

        await CreateService(Catalogue(Record(13, new DateOnly(2025, 8, 19)), Record(14, Volume14Date)))
            .EnrichAsync(series, Run(), Today, Budget(10), expectedBonusVolumes: 2, CancellationToken.None);

        series.Volumes.Count.ShouldBe(13);
    }

    [Fact]
    public async Task EnrichAsync_NoMainCountOnRecord_CreatesNothing()
    {
        // Without the release source's count there is no "past the count" to start from.
        var series = Series(13);
        series.ReleaseSourceVolumeCount = null;

        await CreateService(Catalogue(Record(13, new DateOnly(2025, 8, 19)), Record(14, Volume14Date)))
            .EnrichAsync(series, Run(), Today, Budget(10), expectedBonusVolumes: 2, CancellationToken.None);

        series.Volumes.Count.ShouldBe(13);
    }

    [Fact]
    public async Task EnrichAsync_EveryVolumeEnrichedButTheExtrasMissing_StillAsksTheStore()
    {
        var series = Series(13, enriched: true);

        await CreateService(Catalogue(Record(13, new DateOnly(2025, 8, 19)), Record(14, Volume14Date), Record(15, Volume15Date)))
            .EnrichAsync(series, Run(), Today, Budget(10), expectedBonusVolumes: 2, CancellationToken.None);

        series.Volumes.Count.ShouldBe(15);
    }

    [Fact]
    public async Task EnrichAsync_TheExtrasAlreadyTracked_CreatesNothingMore()
    {
        // 14 and 15 fill the two extras' places, so a store listing a 16 as well adds nothing.
        var series = Series(15, enriched: true);
        var run = Run();

        // A listing past the announcement horizon is neither an extra nor a next volume,
        // which isolates the extras rule.
        await CreateService(Catalogue(Record(15, Volume15Date), Record(16, Today.AddMonths(13))))
            .EnrichAsync(series, run, Today, Budget(10), expectedBonusVolumes: 2, CancellationToken.None);

        series.Volumes.Count.ShouldBe(15);
        run.NewVolumes.ShouldBe(0);
    }

    private static VolumeEnrichmentService CreateService(IVolumeCatalogueSource catalogueSource) =>
        VolumeEnrichmentPipeline.Create(
            catalogueSource,
            NoEnrichment(),
            NoCandidates(),
            VolumeEnrichmentPipeline.Writer(CoverCache()));

    private static CatalogueVolume Record(int number, DateOnly? releaseDate, string medium = "comic") => new()
    {
        SourceRecordKey = $"{medium}-{number}",
        Title = $"{Title}, Vol. {number} ({medium})",
        ReleaseDate = releaseDate,
        ArtworkUrl = new Uri($"https://is1-ssl.mzstatic.com/image/thumb/{medium}{number}.jpg/1000x1500bb.jpg"),
    };

    private static SyncRun Run() => VolumeEnrichmentTestContext.Run(Today);

    /// <summary>
    /// The owner's shelf: the main volumes, in print by MangaUpdates' count of thirteen.
    /// </summary>
    private static Series Series(int volumeCount, bool enriched = false)
    {
        var series = VolumeEnrichmentTestContext.Series(
            Title,
            [.. Enumerable.Range(1, volumeCount).Select(number => enriched ? Enriched(number) : Volume(number))],
            aniListId: 105398);

        series.Format = Format.Manga;
        series.ReleaseSourceVolumeCount = 13;

        return series;
    }

    private static Volume Enriched(int number)
    {
        var volume = Volume(number, isReleased: number <= 13);
        volume.ReleaseDate = new DateOnly(2021, 3, 2);
        volume.CoverObjectKey = "volume/applebooks/known.jpg";
        volume.Title = string.Empty;
        volume.Description = "Already enriched.";

        return volume;
    }
}
