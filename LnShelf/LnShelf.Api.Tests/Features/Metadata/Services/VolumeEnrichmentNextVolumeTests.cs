using static LnShelf.Api.Tests.Features.Metadata.Services.VolumeEnrichmentTestContext;

namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// The store's next volumes inside the catalogue pass: asked for even when every volume is
/// complete, created after the extras, and given their facts in the same pass.
/// </summary>
// Record titles follow Apple Books' own shapes: "(light novel)" for 86, "(comic)" for IZE
// Press' Solo Leveling.
public sealed class VolumeEnrichmentNextVolumeTests
{
    private static readonly DateOnly Today = new(2026, 10, 13);

    public static TheoryData<Format> Formats => [Format.LightNovel, Format.Manga];

    [Theory]
    [MemberData(nameof(Formats))]
    public async Task EnrichAsync_EveryVolumeCompleteAndACountOnRecord_StillAsksTheStore(Format format)
    {
        // The store is where the next volume shows up first, so a complete series is still
        // worth its one request a night.
        var series = Series(format, volumeCount: 13, enriched: true);
        var catalogueSource = Catalogue(Record(format, 13, new DateOnly(2025, 8, 19)));

        await CreateService(catalogueSource)
            .EnrichAsync(series, Run(Today), Today, Budget(10), expectedBonusVolumes: 0, CancellationToken.None);

        await catalogueSource.Received(1)
            .GetVolumesAsync(Arg.Any<string>(), Arg.Any<Format>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public async Task EnrichAsync_TheStoreListsTheNextVolume_CreatesItAndWritesItsFacts(Format format)
    {
        var series = Series(format, volumeCount: 13, enriched: true);
        var date = Today.AddMonths(2);

        var created = await CreateService(Catalogue(Record(format, 13, new DateOnly(2025, 8, 19)), Record(format, 14, date)))
            .EnrichAsync(series, Run(Today), Today, Budget(10), expectedBonusVolumes: 0, CancellationToken.None);

        var volume14 = series.Volumes.Single(volume => volume.Number == 14);
        created.ShouldBe([volume14]);
        volume14.ReleaseDate.ShouldBe(date);
        volume14.ReleaseDateSource.ShouldBe(MetadataSourceName.AppleBooks);
        volume14.IsReleased.ShouldBeFalse();
    }

    [Fact]
    public async Task EnrichAsync_ExtrasThenANextVolume()
    {
        // "13+2": 14 and 15 are the extras the release source counts; 16 is past them, and the
        // store is the first to list it.
        var series = Series(Format.Manga, volumeCount: 13);
        var run = Run(Today);

        var created = await CreateService(Catalogue(
                Record(Format.Manga, 13, new DateOnly(2025, 8, 19)),
                Record(Format.Manga, 14, new DateOnly(2025, 12, 30)),
                Record(Format.Manga, 15, new DateOnly(2026, 3, 17)),
                Record(Format.Manga, 16, Today.AddMonths(3))))
            .EnrichAsync(series, run, Today, Budget(10), expectedBonusVolumes: 2, CancellationToken.None);

        created.Select(volume => volume.Number).ShouldBe([16m]);
        series.Volumes.Max(volume => volume.Number).ShouldBe(16);
        run.NewVolumes.ShouldBe(3);
    }

    [Fact]
    public async Task EnrichAsync_ABonusVolumeHoldsTheExtrasPlaceAndTheStoreDatesVol10_CreatesItAsTheNextVolume()
    {
        // Torture Princess' shape, "9 Volumes + 1 Extra" with Vol. 7.5 tracked: the extras rule
        // grants nothing more, but a dated Vol. 10 is the next volume, the owner-approved
        // "MangaUpdates lags" case.
        var series = Series(Format.LightNovel, volumeCount: 9);
        series.ReleaseSourceVolumeCount = 9;
        series.Volumes.Add(new Volume { Id = Guid.CreateVersion7(), Number = 7.5m, IsReleased = true });

        var created = await CreateService(Catalogue(
                Record(Format.LightNovel, 9, new DateOnly(2024, 1, 1)),
                Record(Format.LightNovel, 10, Today.AddMonths(1))))
            .EnrichAsync(series, Run(Today), Today, Budget(10), expectedBonusVolumes: 1, CancellationToken.None);

        created.Select(volume => volume.Number).ShouldBe([10m]);
    }

    [Fact]
    public async Task EnrichAsync_NoCountOnRecordAndEverythingComplete_DoesNotAskTheStore()
    {
        var series = Series(Format.LightNovel, volumeCount: 13, enriched: true);
        series.ReleaseSourceVolumeCount = null;
        var catalogueSource = Catalogue(Record(Format.LightNovel, 14, Today.AddMonths(1)));

        var created = await CreateService(catalogueSource)
            .EnrichAsync(series, Run(Today), Today, Budget(10), expectedBonusVolumes: 0, CancellationToken.None);

        created.ShouldBeEmpty();
        await catalogueSource.DidNotReceive()
            .GetVolumesAsync(Arg.Any<string>(), Arg.Any<Format>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnrichAsync_UnderAVolumeCountChoice_CreatesNothingAndSkipsTheStoreWhenComplete()
    {
        var series = Series(Format.LightNovel, volumeCount: 13, enriched: true);
        series.SourceChoices.Add(new SourceChoice
        {
            Id = Guid.CreateVersion7(),
            SeriesId = series.Id,
            Field = SyncDiscrepancyField.VolumeCount,
            Source = MetadataSourceName.MangaUpdates,
            ChosenAt = DateTimeOffset.UnixEpoch,
        });
        var catalogueSource = Catalogue(Record(Format.LightNovel, 14, Today.AddMonths(1)));

        var created = await CreateService(catalogueSource)
            .EnrichAsync(series, Run(Today), Today, Budget(10), expectedBonusVolumes: 0, CancellationToken.None);

        created.ShouldBeEmpty();
        series.Volumes.Count.ShouldBe(13);
        await catalogueSource.DidNotReceive()
            .GetVolumesAsync(Arg.Any<string>(), Arg.Any<Format>(), Arg.Any<CancellationToken>());
    }

    private static VolumeEnrichmentService CreateService(IVolumeCatalogueSource catalogueSource) =>
        VolumeEnrichmentPipeline.Create(
            catalogueSource,
            NoEnrichment(),
            NoCandidates(),
            VolumeEnrichmentPipeline.Writer(CoverCache()));

    private static string TitleFor(Format format) => format is Format.Manga ? "Solo Leveling" : "86―EIGHTY-SIX";

    private static CatalogueVolume Record(Format format, int number, DateOnly releaseDate)
    {
        var (title, medium) = format is Format.Manga ? ("Solo Leveling", "comic") : ("86--EIGHTY-SIX", "light novel");

        return new CatalogueVolume
        {
            SourceRecordKey = $"{medium}-{number}",
            Title = $"{title}, Vol. {number} ({medium})",
            ReleaseDate = releaseDate,
            ArtworkUrl = new Uri($"https://is1-ssl.mzstatic.com/image/thumb/{number}.jpg/1000x1500bb.jpg"),
        };
    }

    /// <summary>The main volumes, in print by the release source's count of thirteen.</summary>
    private static Series Series(Format format, int volumeCount, bool enriched = false)
    {
        var series = VolumeEnrichmentTestContext.Series(
            TitleFor(format),
            [.. Enumerable.Range(1, volumeCount).Select(number => enriched ? Enriched(number) : Volume(number))]);

        series.Format = format;
        series.ReleaseSourceVolumeCount = 13;

        return series;
    }

    private static Volume Enriched(int number)
    {
        var volume = Volume(number);
        volume.ReleaseDate = new DateOnly(2021, 3, 2);
        volume.CoverObjectKey = "volume/applebooks/known.jpg";
        volume.Title = string.Empty;
        volume.Description = "Already enriched.";

        return volume;
    }
}
