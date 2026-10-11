using static LnShelf.Api.Tests.Features.Metadata.Services.VolumeEnrichmentTestContext;

namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// What happens when the ISBN the shelf holds is not the printing Open Library indexed —
/// the real Slime case, where five ISBNs 404 while the catalogue plainly holds those
/// volumes under other ISBNs.
/// </summary>
public sealed class VolumeEnrichmentSearchFallbackTests
{
    private const string HeldIsbn = "9781975301156";
    private const string IndexedIsbn = "9781975301163";
    private const string JapaneseIsbn = "9784041030721";

    private static readonly DateOnly Today = new(2026, 7, 31);

    [Fact]
    public async Task EnrichAsync_AHeldIsbnThatIsNotIndexed_FallsBackToAnotherPrintingOfTheSameVolume()
    {
        var series = Series(Volume(4, HeldIsbn));
        var run = Run();

        var enrichmentSource = EnrichmentSourcePerIsbn(
            (HeldIsbn, null),
            (IndexedIsbn, Edition("That Time I Got Reincarnated As a Slime, Vol. 4 (light Novel)", new DateOnly(2018, 12, 11))));

        await CreateService(enrichmentSource, Candidates(IndexedIsbn))
            .EnrichAsync(series, run, Today, new LookupBudget(10), expectedBonusVolumes: 0, CancellationToken.None);

        var volume = series.Volumes.Single();
        volume.ReleaseDate.ShouldBe(new DateOnly(2018, 12, 11));
        // The record's whole name is the series, the number and a medium marker — nothing
        // this volume does not already know — so what is stored is the empty subtitle.
        volume.Title.ShouldBe(string.Empty);

        // Counted as the weaker claim it is: the data came from a name search, not from the
        // identifier the reader gave.
        run.VolumesResolvedBySearch.ShouldBe(1);
        run.EnrichmentGaps.ShouldBe(0);

        // And the reader's own ISBN is left exactly as it was. It records the edition they
        // own; the printing that happened to supply a date is not that.
        volume.Isbn.ShouldBe(HeldIsbn);
    }

    [Fact]
    public async Task EnrichAsync_AFallbackCandidateForTheWrongVolume_IsRefused()
    {
        var series = Series(Volume(4, HeldIsbn));
        var run = Run();

        // The neighbouring volume, which a loose search returns constantly.
        var enrichmentSource = EnrichmentSourcePerIsbn(
            (HeldIsbn, null),
            (IndexedIsbn, Edition("That Time I Got Reincarnated As a Slime 5", new DateOnly(2019, 3, 19))));

        await CreateService(enrichmentSource, Candidates(IndexedIsbn))
            .EnrichAsync(series, run, Today, new LookupBudget(10), expectedBonusVolumes: 0, CancellationToken.None);

        series.Volumes.Single().ReleaseDate.ShouldBeNull();
        run.VolumesResolvedBySearch.ShouldBe(0);
        run.EnrichmentGaps.ShouldBe(1);
    }

    [Fact]
    public async Task EnrichAsync_AFallbackCandidateThatIsTheManga_IsRefused()
    {
        var series = Series(Volume(4, HeldIsbn));
        var run = Run();

        var enrichmentSource = EnrichmentSourcePerIsbn(
            (HeldIsbn, null),
            (IndexedIsbn, Edition(
                "That Time I Got Reincarnated as a Slime 4 (manga)", new DateOnly(2018, 12, 11))));

        await CreateService(enrichmentSource, Candidates(IndexedIsbn))
            .EnrichAsync(series, run, Today, new LookupBudget(10), expectedBonusVolumes: 0, CancellationToken.None);

        // Manga art and a manga date on a light novel is the defect the whole ISBN-first
        // design exists to avoid. Loosening the search must not loosen this.
        series.Volumes.Single().ReleaseDate.ShouldBeNull();
        run.EnrichmentGaps.ShouldBe(1);
    }

    [Fact]
    public async Task EnrichAsync_AJapaneseCandidate_IsNeverEvenFetched()
    {
        var series = Series(Volume(4, HeldIsbn));

        var enrichmentSource = EnrichmentSourcePerIsbn((HeldIsbn, null));

        await CreateService(enrichmentSource, Candidates(JapaneseIsbn))
            .EnrichAsync(series, Run(), Today, new LookupBudget(10), expectedBonusVolumes: 0, CancellationToken.None);

        // Judged from the registration group, which costs no request at all.
        await enrichmentSource.DidNotReceive()
            .GetByIsbnAsync(JapaneseIsbn, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnrichAsync_AVolumeWhoseIsbnResolves_IsNeverSearchedFor()
    {
        var series = Series(Volume(4, HeldIsbn));

        var candidateSource = Candidates(IndexedIsbn);

        await CreateService(
                EnrichmentSourcePerIsbn((HeldIsbn, Edition("Slime, Vol. 4 (light Novel)", new DateOnly(2018, 12, 11)))),
                candidateSource)
            .EnrichAsync(series, Run(), Today, new LookupBudget(10), expectedBonusVolumes: 0, CancellationToken.None);

        // The direct lookup is the cheap, unambiguous path. Searching anyway would spend a
        // request to answer a question already answered.
        await candidateSource.DidNotReceive()
            .FindAsync(Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnrichAsync_TheFallback_SpendsTheBudgetItActuallyUses()
    {
        var series = Series(Volume(4, HeldIsbn));
        var budget = new LookupBudget(10);

        await CreateService(
                EnrichmentSourcePerIsbn((HeldIsbn, null), (IndexedIsbn, Edition("Slime, Vol. 4 (light Novel)", null))),
                Candidates(IndexedIsbn))
            .EnrichAsync(series, Run(), Today, budget, expectedBonusVolumes: 0, CancellationToken.None);

        // One catalogue request for the series, then one direct lookup, one search and one
        // candidate verified for the volume. Counting a volume as a single lookup would let
        // one series quietly spend four times its share against somebody else's donated API.
        budget.Remaining.ShouldBe(6);
    }

    [Fact]
    public async Task EnrichAsync_AnExhaustedBudget_StopsBeforeSearching()
    {
        var series = Series(Volume(4, HeldIsbn));
        var candidateSource = Candidates(IndexedIsbn);

        // Exactly enough for the catalogue request and the direct lookup, and nothing more.
        await CreateService(EnrichmentSourcePerIsbn((HeldIsbn, null)), candidateSource)
            .EnrichAsync(series, Run(), Today, new LookupBudget(2), expectedBonusVolumes: 0, CancellationToken.None);

        await candidateSource.DidNotReceive()
            .FindAsync(Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<CancellationToken>());
    }

    private static VolumeEnrichmentService CreateService(
        IVolumeEnrichmentSource enrichmentSource,
        IIsbnCandidateSource candidateSource) =>
        VolumeEnrichmentPipeline.Create(
            NoCatalogue(),
            enrichmentSource,
            candidateSource,
            VolumeEnrichmentPipeline.Writer());

    private static VolumeEnrichment Edition(string title, DateOnly? releaseDate) => new()
    {
        SourceRecordKey = "/books/OL1M",
        Title = title,
        Publisher = "Yen Press LLC",
        ReleaseDate = releaseDate,
        RawPublishDate = releaseDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        Language = EditionLanguage.English,
    };

    private static SyncRun Run() => VolumeEnrichmentTestContext.Run(Today);

    private static Series Series(params Volume[] volumes) =>
        VolumeEnrichmentTestContext.Series(
            "That Time I Got Reincarnated as a Slime", volumes, aniListId: 86355);
}
