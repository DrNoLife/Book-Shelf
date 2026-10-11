using static LnShelf.Api.Tests.Features.Metadata.Services.VolumeEnrichmentTestContext;

namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// The store arbitrates: the catalogue pass tries each title the series is known by until one
/// is recognised, then remembers the winner.
/// </summary>
// Every title and record string below is real. The shelf holds Konosuba as MangaUpdates files
// it — "Kono Subarashii Sekai ni Shukufuku o!" — and the US storefront answers 0 results for
// that name while carrying all seventeen volumes under "Konosuba: God's Blessing on This
// Wonderful World!". That measured gap is what this pass exists to close, and the English name
// is the *third* alternative MangaUpdates offers, which is why nothing here can stop at one
// attempt or pick a title by rule.
public sealed class VolumeEnrichmentArbitrationTests
{
    private const string ShelfTitle = "Kono Subarashii Sekai ni Shukufuku o!";
    private const string StoreTitle = "Konosuba - God's Blessing on This Wonderful World!";

    private static readonly string[] KonosubaAlternatives =
    [
        "Gifting this Wonderful World with Blessings!",
        "Kono Subarashii Sekai ni Shukufuku wo!",
        StoreTitle,
    ];

    private static readonly DateOnly Today = new(2026, 8, 15);

    [Fact]
    public async Task EnrichAsync_ASeriesTheStoreOnlyKnowsByAnAlternativeTitle_IsEnrichedUnderThatTitle()
    {
        var series = Series(ShelfTitle, KonosubaAlternatives, Volume(1), Volume(2), Volume(3));
        var run = Run();

        await CreateService(KonosubaCatalogue())
            .EnrichAsync(series, run, Today, Budget(10), expectedBonusVolumes: 0, CancellationToken.None);

        // Zero of these were filled before this change, because every one of them hangs off a
        // record the shelf never asked the right question to see.
        series.Volumes.ShouldAllBe(volume => volume.ReleaseDate != null);
        series.Volumes.ShouldAllBe(volume => volume.Description != null);
        series.Volumes.ShouldAllBe(volume => volume.Title != null);
        run.DatesMoved.ShouldBe(3);
        run.DescriptionsWritten.ShouldBe(3);
    }

    [Fact]
    public async Task EnrichAsync_TheTitleTheStoreAnsweredTo_IsRememberedOnTheSeries()
    {
        var series = Series(ShelfTitle, KonosubaAlternatives, Volume(1));

        await CreateService(KonosubaCatalogue())
            .EnrichAsync(series, Run(), Today, Budget(10), expectedBonusVolumes: 0, CancellationToken.None);

        // The learned link. Without it the next sweep would re-spend every attempt to
        // rediscover an answer this run already has.
        series.CatalogueTitle.ShouldBe(StoreTitle);
    }

    [Fact]
    public async Task EnrichAsync_ARememberedTitle_CostsTheOneRequestItUsedTo()
    {
        var series = Series(ShelfTitle, KonosubaAlternatives, Volume(1));
        series.CatalogueTitle = StoreTitle;

        var budget = Budget(10);

        await CreateService(KonosubaCatalogue())
            .EnrichAsync(series, Run(), Today, budget, expectedBonusVolumes: 0, CancellationToken.None);

        // The whole point of storing it: arbitration is a one-off cost, not a nightly one.
        (10 - budget.Remaining).ShouldBe(1);
        series.Volumes.Single().ReleaseDate.ShouldBe(new DateOnly(2017, 2, 21));
    }

    [Fact]
    public async Task EnrichAsync_EveryTitleUntilOneAnswers_IsSpentAndNoMore()
    {
        var series = Series(ShelfTitle, KonosubaAlternatives, Volume(1));
        var budget = Budget(10);

        await CreateService(KonosubaCatalogue())
            .EnrichAsync(series, Run(), Today, budget, expectedBonusVolumes: 0, CancellationToken.None);

        // Four titles, and the winner is the fourth: the shelf's own, then the three
        // alternatives in the source's order. The pass stops the moment one is recognised, so
        // a series whose stored title works still costs exactly one.
        (10 - budget.Remaining).ShouldBe(4);
    }

    [Fact]
    public async Task EnrichAsync_ARecognisedTitleThatIsNotTheFirst_IsNotOutrankedByAnEmptyAnswer()
    {
        var series = Series(ShelfTitle, KonosubaAlternatives, Volume(1));

        await CreateService(KonosubaCatalogue())
            .EnrichAsync(series, Run(), Today, Budget(10), expectedBonusVolumes: 0, CancellationToken.None);

        // "Recognised" is at least one volume confirmed, not a non-empty response. The store
        // answers 74 results for "Gifting this Wonderful World with Blessings!" and not one of
        // them is this series; taking that as the winner would leave the shelf with a
        // remembered title that never matches anything again.
        series.CatalogueTitle.ShouldBe(StoreTitle);
        series.Volumes.Single().Description.ShouldNotBeNull().ShouldContain("goddess");
    }

    [Fact]
    public async Task EnrichAsync_ARememberedTitleTheStoreNoLongerAnswersTo_IsRelearned()
    {
        var series = Series(ShelfTitle, KonosubaAlternatives, Volume(1));

        // A title that worked once and has since stopped — a store re-listing under a new
        // name, which is the ordinary way a storefront's data drifts.
        series.CatalogueTitle = "Konosuba (the store's old listing)";

        await CreateService(KonosubaCatalogue())
            .EnrichAsync(series, Run(), Today, Budget(10), expectedBonusVolumes: 0, CancellationToken.None);

        // The remembered title leads the list but does not end it, which is what makes the
        // stored column self-healing rather than a permanent wrong answer.
        series.CatalogueTitle.ShouldBe(StoreTitle);
        series.Volumes.Single().ReleaseDate.ShouldBe(new DateOnly(2017, 2, 21));
    }

    [Fact]
    public async Task EnrichAsync_NoTitleTheStoreRecognises_ForgetsTheRememberedOne()
    {
        var series = Series(ShelfTitle, KonosubaAlternatives, Volume(1));
        series.CatalogueTitle = StoreTitle;

        // The store answers, and about nothing this series is known by.
        await CreateService(CatalogueAnswering([]))
            .EnrichAsync(series, Run(), Today, Budget(10), expectedBonusVolumes: 0, CancellationToken.None);

        // Cleared rather than kept: a learned link that no longer resolves is worse than none,
        // because it would go on being tried first every night.
        series.CatalogueTitle.ShouldBeNull();
    }

    [Fact]
    public async Task EnrichAsync_AStoreThatCannotBeReached_KeepsWhatTheShelfLearned()
    {
        var series = Series(ShelfTitle, KonosubaAlternatives, Volume(1));
        series.CatalogueTitle = StoreTitle;

        var run = Run();

        var catalogueSource = Substitute.For<IVolumeCatalogueSource>();
        catalogueSource.SourceName.Returns("AppleBooks");
        catalogueSource.GetVolumesAsync(Arg.Any<string>(), Arg.Any<Format>(), Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<CatalogueVolume>>>(_ => throw new HttpRequestException("Apple is down"));

        await CreateService(catalogueSource)
            .EnrichAsync(series, run, Today, Budget(10), expectedBonusVolumes: 0, CancellationToken.None);

        // An outage is not evidence against the title. Forgetting it here would make a night
        // Apple was down cost every series its learned name, and the next run would re-spend
        // the whole attempt list for all of them.
        series.CatalogueTitle.ShouldBe(StoreTitle);

        // One failure per attempt, isolated exactly as the per-volume loop isolates its own.
        run.EnrichmentFailures.ShouldBe(4);
    }

    [Fact]
    public async Task EnrichAsync_ABudgetThatRunsOutMidList_StopsWithoutForgettingAnything()
    {
        var series = Series(ShelfTitle, KonosubaAlternatives, Volume(1));
        series.CatalogueTitle = "Konosuba (the store's old listing)";

        // Enough for the remembered title and one more, and the winner is further down.
        await CreateService(KonosubaCatalogue())
            .EnrichAsync(series, Run(), Today, Budget(2), expectedBonusVolumes: 0, CancellationToken.None);

        // The remembered title did answer, and with nothing — but the list was never finished,
        // so there is no new title to learn and the old one is the best guess still. Nulling
        // it here would throw away a link on account of a budget.
        series.CatalogueTitle.ShouldBe("Konosuba (the store's old listing)");
        series.Volumes.Single().ReleaseDate.ShouldBeNull();
    }

    [Fact]
    public async Task EnrichAsync_ASeriesWithNoAlternativesAtAll_BehavesExactlyAsItDid()
    {
        var series = Series("86―EIGHTY-SIX", [], Volume(8));
        var budget = Budget(10);

        await CreateService(CatalogueAnswering(
                [Record("86--EIGHTY-SIX, Vol. 8 (light novel)", new DateOnly(2021, 8, 24))],
                "86―EIGHTY-SIX"))
            .EnrichAsync(series, Run(), Today, budget, expectedBonusVolumes: 0, CancellationToken.None);

        // The AniList-added shelf, which is most of it: one title, one request, and the
        // learned column filled in on the way past.
        (10 - budget.Remaining).ShouldBe(1);
        series.CatalogueTitle.ShouldBe("86―EIGHTY-SIX");
        series.Volumes.Single().ReleaseDate.ShouldBe(new DateOnly(2021, 8, 24));
    }

    /// <summary>
    /// The store as it actually answers for this series: nothing for the romaji names, a bag
    /// of unrelated books for the German-market one, and the whole shelf for the English one.
    /// </summary>
    private static IVolumeCatalogueSource KonosubaCatalogue() => CatalogueByTitle(new()
    {
        // Measured live, 2026-08-15: 0 results for either romaji spelling.
        [ShelfTitle] = [],
        ["Kono Subarashii Sekai ni Shukufuku wo!"] = [],

        // 74 results, none of them this series. A real one of them, so the "non-empty is not
        // recognition" rule is exercised against what the store really sends.
        ["Gifting this Wonderful World with Blessings!"] = [Record("Oil In Your Gifting", null)],

        [StoreTitle] =
        [
            Record(
                "Konosuba: God's Blessing on This Wonderful World!, Vol. 1 (light novel)",
                new DateOnly(2017, 2, 21),
                "A shut-in is offered a goddess and a fresh start in another world."),
            Record(
                "Konosuba: God's Blessing on This Wonderful World!, Vol. 2 (light novel)",
                new DateOnly(2017, 4, 18),
                "A goddess, a crusader and a bounty nobody wants to collect."),
            Record(
                "Konosuba: God's Blessing on This Wonderful World!, Vol. 3 (light novel)",
                new DateOnly(2017, 8, 22),
                "The goddess of water finally arrives, and so does winter."),
        ],
    });

    /// <param name="onlyFor">
    /// The one title this store answers under. Null means it answers the same for every title,
    /// which is how "the store knows nothing about this series" is written.
    /// </param>
    private static IVolumeCatalogueSource CatalogueAnswering(CatalogueVolume[] records, string? onlyFor = null)
    {
        var answers = new Dictionary<string, CatalogueVolume[]>();

        if (onlyFor is not null)
        {
            answers[onlyFor] = records;
        }

        return CatalogueByTitle(answers);
    }

    private static CatalogueVolume Record(string title, DateOnly? releaseDate, string? description = null) => new()
    {
        SourceRecordKey = "1181694347",
        Title = title,
        Description = description,
        ReleaseDate = releaseDate,
        ArtworkUrl = null,
    };

    private static VolumeEnrichmentService CreateService(IVolumeCatalogueSource catalogueSource) =>
        VolumeEnrichmentPipeline.Create(
            catalogueSource,
            NoEnrichment(),
            NoCandidates(),
            VolumeEnrichmentPipeline.Writer());

    private static SyncRun Run() => VolumeEnrichmentTestContext.Run(Today);

    private static Series Series(string titleEnglish, string[] alternativeTitles, params Volume[] volumes) =>
        VolumeEnrichmentTestContext.Series(
            titleEnglish,
            volumes,
            mangaUpdatesId: 48702062318,
            alternativeTitles: alternativeTitles,
            publicationStatus: PublicationStatus.Completed);
}
