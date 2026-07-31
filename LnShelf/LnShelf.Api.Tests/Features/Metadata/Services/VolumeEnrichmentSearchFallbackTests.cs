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

        var enrichmentSource = EnrichmentSource(
            (HeldIsbn, null),
            (IndexedIsbn, Edition("That Time I Got Reincarnated As a Slime, Vol. 4 (light Novel)", new DateOnly(2018, 12, 11))));

        await CreateService(enrichmentSource, Candidates(IndexedIsbn))
            .EnrichAsync(series, run, Today, new LookupBudget(10), CancellationToken.None);

        var volume = series.Volumes.Single();
        volume.ReleaseDate.ShouldBe(new DateOnly(2018, 12, 11));
        volume.Title.ShouldBe("That Time I Got Reincarnated As a Slime, Vol. 4 (light Novel)");

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
        var enrichmentSource = EnrichmentSource(
            (HeldIsbn, null),
            (IndexedIsbn, Edition("That Time I Got Reincarnated As a Slime 5", new DateOnly(2019, 3, 19))));

        await CreateService(enrichmentSource, Candidates(IndexedIsbn))
            .EnrichAsync(series, run, Today, new LookupBudget(10), CancellationToken.None);

        series.Volumes.Single().ReleaseDate.ShouldBeNull();
        run.VolumesResolvedBySearch.ShouldBe(0);
        run.EnrichmentGaps.ShouldBe(1);
    }

    [Fact]
    public async Task EnrichAsync_AFallbackCandidateThatIsTheManga_IsRefused()
    {
        var series = Series(Volume(4, HeldIsbn));
        var run = Run();

        var enrichmentSource = EnrichmentSource(
            (HeldIsbn, null),
            (IndexedIsbn, Edition(
                "That Time I Got Reincarnated as a Slime 4 (manga)", new DateOnly(2018, 12, 11))));

        await CreateService(enrichmentSource, Candidates(IndexedIsbn))
            .EnrichAsync(series, run, Today, new LookupBudget(10), CancellationToken.None);

        // Manga art and a manga date on a light novel is the defect the whole ISBN-first
        // design exists to avoid. Loosening the search must not loosen this.
        series.Volumes.Single().ReleaseDate.ShouldBeNull();
        run.EnrichmentGaps.ShouldBe(1);
    }

    [Fact]
    public async Task EnrichAsync_AJapaneseCandidate_IsNeverEvenFetched()
    {
        var series = Series(Volume(4, HeldIsbn));

        var enrichmentSource = EnrichmentSource((HeldIsbn, null));

        await CreateService(enrichmentSource, Candidates(JapaneseIsbn))
            .EnrichAsync(series, Run(), Today, new LookupBudget(10), CancellationToken.None);

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
                EnrichmentSource((HeldIsbn, Edition("Slime, Vol. 4 (light Novel)", new DateOnly(2018, 12, 11)))),
                candidateSource)
            .EnrichAsync(series, Run(), Today, new LookupBudget(10), CancellationToken.None);

        // The direct lookup is the cheap, unambiguous path. Searching anyway would spend a
        // request to answer a question already answered.
        await candidateSource.DidNotReceive()
            .FindAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnrichAsync_TheFallback_SpendsTheBudgetItActuallyUses()
    {
        var series = Series(Volume(4, HeldIsbn));
        var budget = new LookupBudget(10);

        await CreateService(
                EnrichmentSource((HeldIsbn, null), (IndexedIsbn, Edition("Slime, Vol. 4 (light Novel)", null))),
                Candidates(IndexedIsbn))
            .EnrichAsync(series, Run(), Today, budget, CancellationToken.None);

        // One direct lookup, one search, one candidate verified. Counting a volume as a
        // single lookup would let one series quietly spend four times its share against
        // somebody else's donated API.
        budget.Remaining.ShouldBe(7);
    }

    [Fact]
    public async Task EnrichAsync_AnExhaustedBudget_StopsBeforeSearching()
    {
        var series = Series(Volume(4, HeldIsbn));
        var candidateSource = Candidates(IndexedIsbn);

        // Exactly enough for the direct lookup and nothing more.
        await CreateService(EnrichmentSource((HeldIsbn, null)), candidateSource)
            .EnrichAsync(series, Run(), Today, new LookupBudget(1), CancellationToken.None);

        await candidateSource.DidNotReceive()
            .FindAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    private static VolumeEnrichmentService CreateService(
        IVolumeEnrichmentSource enrichmentSource,
        IIsbnCandidateSource candidateSource) =>
        new(
            enrichmentSource,
            candidateSource,
            Substitute.For<IVolumeCoverCache>(),
            NullLogger<VolumeEnrichmentService>.Instance);

    private static IIsbnCandidateSource Candidates(params string[] isbns)
    {
        var source = Substitute.For<IIsbnCandidateSource>();
        source.SourceName.Returns("OpenLibrary");
        source.FindAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new IsbnCandidates { WorksFound = 1, Isbns = isbns });

        return source;
    }

    private static IVolumeEnrichmentSource EnrichmentSource(
        params (string Isbn, VolumeEnrichment? Edition)[] editions)
    {
        var source = Substitute.For<IVolumeEnrichmentSource>();
        source.SourceName.Returns("OpenLibrary");
        source.GetByIsbnAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((VolumeEnrichment?)null);

        foreach (var (isbn, edition) in editions)
        {
            source.GetByIsbnAsync(isbn, Arg.Any<CancellationToken>()).Returns(edition);
        }

        return source;
    }

    private static VolumeEnrichment Edition(string title, DateOnly? releaseDate) => new()
    {
        SourceRecordKey = "/books/OL1M",
        Title = title,
        Publisher = "Yen Press LLC",
        ReleaseDate = releaseDate,
        RawPublishDate = releaseDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        Language = EditionLanguage.English,
    };

    private static SyncRun Run() => new()
    {
        Id = Guid.CreateVersion7(),
        StartedAt = new DateTimeOffset(Today, TimeOnly.MinValue, TimeSpan.Zero),
    };

    private static Series Series(params Volume[] volumes)
    {
        var series = new Series
        {
            Id = Guid.CreateVersion7(),
            AniListId = 86355,
            TitleEnglish = "That Time I Got Reincarnated as a Slime",
            Format = Format.LightNovel,
            PublicationStatus = PublicationStatus.Ongoing,
        };

        foreach (var volume in volumes)
        {
            series.Volumes.Add(volume);
        }

        return series;
    }

    private static Volume Volume(int number, string isbn) => new()
    {
        Id = Guid.CreateVersion7(),
        Number = number,
        Isbn = isbn,
        IsReleased = true,
    };
}
