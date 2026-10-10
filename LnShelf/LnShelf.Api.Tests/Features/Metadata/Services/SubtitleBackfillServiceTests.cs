using Microsoft.EntityFrameworkCore;

namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// The one-shot subtitle pass: which volumes it asks about, and the single column it is
/// allowed to move. Only <c>"" → a subtitle</c>, never over a real one and never over null.
/// </summary>
public sealed class SubtitleBackfillServiceTests
{
    private const string EnglishIsbn = "9780316363983";
    private const string SecondEnglishIsbn = "9780316363921";
    private const string JapaneseIsbn = "9784041030721";

    [Fact]
    public async Task RunAsync_OnlyVolumesWithAnIsbnAndAnEmptyTitle_AreAskedAbout()
    {
        await using var dbContext = CreateDbContext();
        await SeedAsync(dbContext,
        [
            (1, EnglishIsbn, string.Empty),                      // the target
            (2, SecondEnglishIsbn, null),                        // unresolved: the sync's job
            (3, "9780316472240", "The Bloody Valkyrie"),         // a real subtitle already
            (4, null, string.Empty),                             // nothing to look up by
        ]);

        var source = Enrichments((EnglishIsbn, "Overlord, Vol. 1: The Undead King", EditionLanguage.English));

        var report = await CreateService(dbContext, source).RunAsync(CancellationToken.None);

        report.Asked.ShouldBe(1);
        await source.Received(1).GetByIsbnAsync(EnglishIsbn, Arg.Any<CancellationToken>());
        await source.DidNotReceive().GetByIsbnAsync(SecondEnglishIsbn, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_ARecordCarryingASubtitle_WritesIt()
    {
        await using var dbContext = CreateDbContext();
        await SeedAsync(dbContext, [(3, EnglishIsbn, string.Empty)]);

        var report = await CreateService(
                dbContext,
                Enrichments((EnglishIsbn, "Overlord, Vol. 3: The Bloody Valkyrie", EditionLanguage.English)))
            .RunAsync(CancellationToken.None);

        // The upgrade VolumeFactWriter's title rule allows and this command exists to trigger:
        // the store named the volume and had no subtitle, so "" was as far as the nightly pass
        // could get.
        (await dbContext.Volumes.SingleAsync()).Title.ShouldBe("The Bloody Valkyrie");
        report.Subtitled.ShouldBe(1);
        report.WithoutSubtitle.ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_ARecordWithNoSubtitleOfItsOwn_LeavesTheTitleEmpty()
    {
        await using var dbContext = CreateDbContext();
        await SeedAsync(dbContext, [(16, EnglishIsbn, string.Empty)]);

        var report = await CreateService(
                dbContext,
                Enrichments((EnglishIsbn, "Overlord, Vol. 16", EditionLanguage.English)))
            .RunAsync(CancellationToken.None);

        // Two sources now agree the volume has no subtitle, and "" already says so. Writing
        // "" over "" would be a no-op; the point of the assertion is that nothing else — a
        // null, or the record's whole name — lands here instead.
        (await dbContext.Volumes.SingleAsync()).Title.ShouldBe(string.Empty);
        report.WithoutSubtitle.ShouldBe(1);
        report.Subtitled.ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_AJapaneseMarketIsbn_IsNotAskedAbout()
    {
        await using var dbContext = CreateDbContext();
        await SeedAsync(dbContext, [(1, JapaneseIsbn, string.Empty)]);

        var source = Enrichments();

        var report = await CreateService(dbContext, source).RunAsync(CancellationToken.None);

        // The same skip the sync makes, from the ISBN rather than the record: it resolves to a
        // Japanese-market edition, whose title is the wrong title for an English shelf entry.
        report.Asked.ShouldBe(0);
        await source.DidNotReceive().GetByIsbnAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_ARecordNamingAnotherLanguage_WritesNothing()
    {
        await using var dbContext = CreateDbContext();
        await SeedAsync(dbContext, [(3, EnglishIsbn, string.Empty)]);

        // A Brazilian or French edition sharing a work with the English one. The ISBN cannot
        // rule it out, so the record's own language field has to.
        var report = await CreateService(
                dbContext,
                Enrichments((EnglishIsbn, "Overlord, Vol. 3: O Rei Morto-Vivo", EditionLanguage.Other)))
            .RunAsync(CancellationToken.None);

        (await dbContext.Volumes.SingleAsync()).Title.ShouldBe(string.Empty);
        report.NotIndexed.ShouldBe(1);
        report.Subtitled.ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_NoRecordForTheIsbn_CountsAsNotIndexed()
    {
        await using var dbContext = CreateDbContext();
        await SeedAsync(dbContext, [(3, EnglishIsbn, string.Empty)]);

        var report = await CreateService(dbContext, Enrichments()).RunAsync(CancellationToken.None);

        // The most common miss, and not an error: the printing the reader owns is often
        // simply not the printing a library catalogued.
        report.Asked.ShouldBe(1);
        report.NotIndexed.ShouldBe(1);
        (await dbContext.Volumes.SingleAsync()).Title.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task RunAsync_OneVolumesLookupThrowing_DoesNotCostTheOthers()
    {
        await using var dbContext = CreateDbContext();
        await SeedAsync(dbContext, [(3, EnglishIsbn, string.Empty), (4, SecondEnglishIsbn, string.Empty)]);

        var source = Enrichments((SecondEnglishIsbn, "Overlord, Vol. 4: The Lizardman Heroes", EditionLanguage.English));
        source.GetByIsbnAsync(EnglishIsbn, Arg.Any<CancellationToken>())
            .Returns<Task<VolumeEnrichment?>>(_ => throw new HttpRequestException("Open Library is down"));

        var report = await CreateService(dbContext, source).RunAsync(CancellationToken.None);

        // Per-volume failure isolation, the rule the sync run and the discovery command both
        // follow — and here it also protects the lookups already spent.
        report.Failed.ShouldBe(1);
        report.Subtitled.ShouldBe(1);
        (await dbContext.Volumes.SingleAsync(volume => volume.Number == 4)).Title
            .ShouldBe("The Lizardman Heroes");
    }

    [Fact]
    public async Task RunAsync_RunTwice_AsksOnlyAboutWhatIsStillEmpty()
    {
        await using var dbContext = CreateDbContext();
        await SeedAsync(dbContext,
        [
            (3, EnglishIsbn, string.Empty),
            (16, SecondEnglishIsbn, string.Empty),
        ]);

        var source = Enrichments(
            (EnglishIsbn, "Overlord, Vol. 3: The Bloody Valkyrie", EditionLanguage.English),
            (SecondEnglishIsbn, "Overlord, Vol. 16", EditionLanguage.English));

        await CreateService(dbContext, source).RunAsync(CancellationToken.None);
        var second = await CreateService(dbContext, source).RunAsync(CancellationToken.None);

        // The documented limit of the command's idempotency: the subtitled volume drops out
        // of the eligible set for good, and the one whose record has no subtitle stays "" and
        // is asked again. Accepted for a hand-run one-shot rather than fixed with a marker
        // state — see SubtitleBackfillService.
        second.Asked.ShouldBe(1);
        second.Subtitled.ShouldBe(0);
        second.WithoutSubtitle.ShouldBe(1);
    }

    [Fact]
    public async Task RunAsync_VolumesOfTwoSeries_AreCountedPerSeries()
    {
        await using var dbContext = CreateDbContext();
        await SeedAsync(dbContext, [(3, EnglishIsbn, string.Empty)]);
        await SeedAsync(dbContext, [(2, SecondEnglishIsbn, string.Empty)], "86―EIGHTY-SIX");

        var report = await CreateService(
                dbContext,
                Enrichments(
                    (EnglishIsbn, "Overlord, Vol. 3: The Bloody Valkyrie", EditionLanguage.English),
                    (SecondEnglishIsbn, "86--EIGHTY-SIX, Vol. 2: Run Through the Battlefront (Start)",
                        EditionLanguage.English)))
            .RunAsync(CancellationToken.None);

        // Per series, because that is the grain the report is read at.
        report.Series.Count.ShouldBe(2);
        report.Series.Single(result => result.SeriesTitle is "Overlord").Subtitled.ShouldBe(1);
        report.Series.Single(result => result.SeriesTitle is "86―EIGHTY-SIX").Subtitled.ShouldBe(1);
        report.Subtitled.ShouldBe(2);
    }

    private static SubtitleBackfillService CreateService(
        LnShelfDbContext dbContext,
        IVolumeEnrichmentSource enrichmentSource) =>
        new(dbContext, enrichmentSource, NullLogger<SubtitleBackfillService>.Instance);

    /// <summary>A source that knows exactly these editions and nothing else.</summary>
    private static IVolumeEnrichmentSource Enrichments(
        params (string Isbn, string Title, EditionLanguage Language)[] editions)
    {
        var source = Substitute.For<IVolumeEnrichmentSource>();
        source.SourceName.Returns("OpenLibrary");
        source.GetByIsbnAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((VolumeEnrichment?)null);

        foreach (var (isbn, title, language) in editions)
        {
            source.GetByIsbnAsync(isbn, Arg.Any<CancellationToken>()).Returns(new VolumeEnrichment
            {
                SourceRecordKey = $"/books/{isbn}",
                Title = title,
                Publisher = "Yen On",
                Language = language,
            });
        }

        return source;
    }

    private static async Task SeedAsync(
        LnShelfDbContext dbContext,
        (int Number, string? Isbn, string? Title)[] volumes,
        string seriesTitle = "Overlord")
    {
        var series = new Series
        {
            Id = Guid.CreateVersion7(),
            AniListId = 85976,
            TitleEnglish = seriesTitle,
            Format = Format.LightNovel,
            PublicationStatus = PublicationStatus.Ongoing,
        };

        foreach (var (number, isbn, title) in volumes)
        {
            series.Volumes.Add(new Volume
            {
                Id = Guid.CreateVersion7(),
                Number = number,
                Isbn = isbn,
                Title = title,
            });
        }

        dbContext.Series.Add(series);
        await dbContext.SaveChangesAsync();
    }

    private static LnShelfDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<LnShelfDbContext>()
            .UseInMemoryDatabase($"subtitle-backfill-{Guid.CreateVersion7()}")
            .Options, TestUsers.Current);
}
