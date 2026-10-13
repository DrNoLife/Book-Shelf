using LnShelf.Api.Tests.Features.Metadata.Fixtures;
using static LnShelf.Api.Tests.Features.Metadata.Services.VolumeEnrichmentTestContext;

namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// The store's next volumes: whole volumes straight past the highest main volume tracked, the
/// third kind the catalogue may create (86's Vol. 14, listed while MangaUpdates counts 13).
/// </summary>
public sealed class NextMainVolumeWriterTests
{
    private const string Title = "86―EIGHTY-SIX";

    private static readonly DateOnly Today = new(2026, 10, 13);

    [Fact]
    public void Create_TheStoresPreOrderOfTheNextVolume_CreatesItUnreleased()
    {
        var series = Series(count: 13, Enumerable.Range(1, 13));
        var run = Run(Today);

        var created = Create(series, [.. AppleBooksFixtures.LoadCatalogue(AppleBooksFixtures.EightySixSearch)], run);

        var volume = created.ShouldHaveSingleItem();
        volume.Number.ShouldBe(14);
        volume.IsReleased.ShouldBeFalse();
        volume.IsLocalOnly.ShouldBeFalse();
        series.Volumes.ShouldContain(volume);
        run.NewVolumes.ShouldBe(1);
    }

    [Fact]
    public void Create_ContiguousListings_CreatesEachInOrder()
    {
        var series = Series(count: 13, Enumerable.Range(1, 13));

        var created = Create(series, [Record(14, Today.AddMonths(1)), Record(15, Today.AddMonths(4)), Record(16, Today.AddMonths(7))]);

        created.Select(volume => volume.Number).ShouldBe([14m, 15m, 16m]);
    }

    [Fact]
    public void Create_AGapInTheListing_StopsAtTheGap()
    {
        // A store listing 16 without 15 is not grounds to guess where 15 went.
        var series = Series(count: 13, Enumerable.Range(1, 13));

        var created = Create(series, [Record(14, Today.AddMonths(1)), Record(16, Today.AddMonths(7))]);

        created.Select(volume => volume.Number).ShouldBe([14m]);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void Create_ADateAgainstTheHorizon_IsCreatedOnlyWithinIt(int daysPastTheHorizon, bool expected)
    {
        // Twelve months ahead is an announcement; a day later is a placeholder.
        var series = Series(count: 13, Enumerable.Range(1, 13));
        var date = Today.AddMonths(NextMainVolumeWriter.AnnouncementHorizonMonths).AddDays(daysPastTheHorizon);

        var created = Create(series, [Record(14, date)]);

        (created.Count == 1).ShouldBe(expected);
    }

    [Fact]
    public void Create_AnUndatedListing_StopsThere()
    {
        var series = Series(count: 13, Enumerable.Range(1, 13));

        var created = Create(series, [Record(14, Today.AddMonths(1)), Record(15, releaseDate: null), Record(16, Today.AddMonths(7))]);

        created.Select(volume => volume.Number).ShouldBe([14m]);
    }

    [Fact]
    public void Create_TheOtherMediumsRecord_IsNotCreated()
    {
        var series = Series(count: 13, Enumerable.Range(1, 13));

        var created = Create(series, [Record(14, Today.AddMonths(1), medium: "manga")]);

        created.ShouldBeEmpty();
        series.Volumes.Count.ShouldBe(13);
    }

    [Fact]
    public void Create_UnderAVolumeCountChoice_CreatesNothing()
    {
        // The user settled the count by hand; the store does not reopen it.
        var series = Series(count: 13, Enumerable.Range(1, 13));
        AddVolumeCountChoice(series);

        var created = Create(series, [Record(14, Today.AddMonths(1))]);

        created.ShouldBeEmpty();
    }

    [Fact]
    public void Create_NoReleaseSourceCount_CreatesNothing()
    {
        // An unlinked or uncounted series never grows from the store alone.
        var series = Series(count: null, Enumerable.Range(1, 13));

        var created = Create(series, [Record(14, Today.AddMonths(1))]);

        created.ShouldBeEmpty();
    }

    [Fact]
    public void Create_StartsPastTheHighestMainVolume_NotPastABonusVolume()
    {
        var series = Series(count: 9, Enumerable.Range(1, 9));
        series.Volumes.Add(new Volume { Id = Guid.CreateVersion7(), Number = 9.5m, IsReleased = true });

        var created = Create(series, [Record(9, new DateOnly(2024, 1, 1)), Record(10, Today.AddMonths(2))]);

        created.Select(volume => volume.Number).ShouldBe([10m]);
    }

    [Fact]
    public void Create_StartsPastVolumesBeyondTheCount()
    {
        // AniList or a manual add already took the shelf to 18; the store adds only what is
        // past that.
        var series = Series(count: 16, Enumerable.Range(1, 18));

        var created = Create(series, [Record(17, Today.AddMonths(-3)), Record(18, Today.AddMonths(-1)), Record(19, Today.AddMonths(2))]);

        created.Select(volume => volume.Number).ShouldBe([19m]);
    }

    [Fact]
    public void Create_LeavesEveryExistingVolumeAsItWas()
    {
        var series = Series(count: 13, Enumerable.Range(1, 13));
        var before = Facts(series);

        Create(series, [Record(13, new DateOnly(2025, 1, 1)), Record(14, Today.AddMonths(1))]);

        Facts(series).Where(facts => facts.Number <= 13).ShouldBe(before);
    }

    [Theory]
    [InlineData(13, false, true)]
    [InlineData(null, false, false)]
    [InlineData(13, true, false)]
    public void MayCreate_TheCountAndTheChoice_DecideIt(int? count, bool volumeCountChoice, bool expected)
    {
        var series = Series(count, Enumerable.Range(1, 3));

        if (volumeCountChoice)
        {
            AddVolumeCountChoice(series);
        }

        NextMainVolumeWriter.MayCreate(series).ShouldBe(expected);
    }

    private static IReadOnlyList<Volume> Create(Series series, CatalogueVolume[] catalogue, SyncRun? run = null)
    {
        var match = new CatalogueVolumeMatcher(NullLogger<CatalogueVolumeMatcher>.Instance)
            .Match(series, series.TitleEnglish, catalogue);

        return new NextMainVolumeWriter(NullLogger<NextMainVolumeWriter>.Instance)
            .Create(series, match, Today, run ?? Run(Today));
    }

    private static CatalogueVolume Record(int number, DateOnly? releaseDate, string medium = "light novel") => new()
    {
        SourceRecordKey = $"{medium}-{number}",
        Title = $"{Title}, Vol. {number} ({medium})",
        ReleaseDate = releaseDate,
    };

    private static Series Series(int? count, IEnumerable<int> volumeNumbers)
    {
        var series = VolumeEnrichmentTestContext.Series(Title, [.. volumeNumbers.Select(number => Volume(number))]);
        series.ReleaseSourceVolumeCount = count;

        return series;
    }

    private static void AddVolumeCountChoice(Series series) => series.SourceChoices.Add(new SourceChoice
    {
        Id = Guid.CreateVersion7(),
        SeriesId = series.Id,
        Field = SyncDiscrepancyField.VolumeCount,
        Source = MetadataSourceName.MangaUpdates,
        ChosenAt = DateTimeOffset.UnixEpoch,
    });

    private static List<(decimal Number, bool IsReleased, DateOnly? ReleaseDate, bool IsLocalOnly)> Facts(Series series) =>
    [
        .. series.Volumes
            .OrderBy(volume => volume.Number)
            .Select(volume => (volume.Number, volume.IsReleased, volume.ReleaseDate, volume.IsLocalOnly)),
    ];
}
