using LnShelf.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// The ISBN discovery pipeline: which volumes it asks about, and which candidate it
/// believes. It proposes into a file and writes nothing to the shelf, which is the
/// property that matters most here.
/// </summary>
public sealed class IsbnDiscoveryServiceTests
{
    private const string EnglishIsbn = "9780316363921";
    private const string JapaneseIsbn = "9784041030721";

    [Fact]
    public async Task RunAsync_OnlyOwnedVolumesWithoutAnIsbn_AreAskedAbout()
    {
        await using var dbContext = CreateDbContext();
        await SeedAsync(dbContext,
        [
            (1, "9780316272247", true),   // already carries one
            (2, null, true),              // the target
            (3, null, false),             // not owned: Open Library catalogues the past
        ]);

        var rows = await CreateService(dbContext, Candidates(), Enrichments()).RunAsync(CancellationToken.None);

        var row = rows.ShouldHaveSingleItem();
        row.VolumeNumber.ShouldBe(2);
        row.SeriesTitle.ShouldBe("Overlord");
    }

    [Fact]
    public async Task RunAsync_ARecordNamingEnglish_IsProposed()
    {
        await using var dbContext = CreateDbContext();
        await SeedAsync(dbContext, [(2, null, true)]);

        // The real search result for "Overlord volume 2": English and Japanese editions
        // in one undifferentiated pile, Japanese listed first so that order cannot be
        // what decides it.
        var rows = await CreateService(
                dbContext,
                Candidates(JapaneseIsbn, EnglishIsbn),
                Enrichments(
                    (JapaneseIsbn, EditionLanguage.Other, "Kadokawa"),
                    (EnglishIsbn, EditionLanguage.English, "Yen On")))
            .RunAsync(CancellationToken.None);

        var row = rows.ShouldHaveSingleItem();
        row.Isbn.ShouldBe(EnglishIsbn);
        row.Outcome.ShouldBe(IsbnDiscoveryOutcome.ConfirmedEnglish);
        row.Publisher.ShouldBe("Yen On");
        row.OpenLibraryTitle.ShouldNotBeNull();
    }

    [Fact]
    public async Task RunAsync_NoRecordNamingEnglishButAKnownImprint_IsProposedAsLikely()
    {
        await using var dbContext = CreateDbContext();
        await SeedAsync(dbContext, [(2, null, true)]);

        // The common shape: a genuinely English record that carries no language field at
        // all. Refusing every one of those would throw away real editions.
        var rows = await CreateService(
                dbContext,
                Candidates(EnglishIsbn),
                Enrichments((EnglishIsbn, EditionLanguage.Unstated, "Yen Press LLC")))
            .RunAsync(CancellationToken.None);

        var row = rows.ShouldHaveSingleItem();
        row.Isbn.ShouldBe(EnglishIsbn);
        row.Outcome.ShouldBe(IsbnDiscoveryOutcome.LikelyEnglishPublisher);
        row.Note.ShouldContain("check the title");
    }

    [Fact]
    public async Task RunAsync_AJapaneseIsbnWithNoLanguageOnTheRecord_IsNotProposed()
    {
        await using var dbContext = CreateDbContext();
        await SeedAsync(dbContext, [(1, null, true)]);

        // The real Slime volume 1 trap: a Japanese ISBN whose Open Library record is a
        // French edition stating no language at all. The record cannot rule it out, so
        // the ISBN's own registration group has to.
        var rows = await CreateService(
                dbContext,
                Candidates(JapaneseIsbn),
                Enrichments((JapaneseIsbn, EditionLanguage.Unstated, "MICROMAGAZINE")))
            .RunAsync(CancellationToken.None);

        var row = rows.ShouldHaveSingleItem();
        row.Isbn.ShouldBeNull();
        row.Outcome.ShouldBe(IsbnDiscoveryOutcome.NoEnglishEdition);
    }

    [Fact]
    public async Task RunAsync_ASearchThatFindsNothing_StillWritesARow()
    {
        await using var dbContext = CreateDbContext();
        await SeedAsync(dbContext, [(20, null, true)]);

        var rows = await CreateService(dbContext, Candidates(), Enrichments()).RunAsync(CancellationToken.None);

        // A review file that silently omits its failures reads as "these are all of them".
        var row = rows.ShouldHaveSingleItem();
        row.Outcome.ShouldBe(IsbnDiscoveryOutcome.NoSearchResults);
        row.Isbn.ShouldBeNull();
    }

    [Fact]
    public async Task RunAsync_WorksFoundButNoneListingAnIsbn_SaysSoDistinctly()
    {
        await using var dbContext = CreateDbContext();
        await SeedAsync(dbContext, [(2, null, true)]);

        var candidateSource = Substitute.For<IIsbnCandidateSource>();
        candidateSource.SourceName.Returns("OpenLibrary");
        candidateSource.FindAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new IsbnCandidates { WorksFound = 3, Isbns = [] });

        var rows = await CreateService(dbContext, candidateSource, Enrichments()).RunAsync(CancellationToken.None);

        // A different problem from "not catalogued", with a different fix, so it gets a
        // different word in the review file.
        rows.ShouldHaveSingleItem().Outcome.ShouldBe(IsbnDiscoveryOutcome.NoCandidateIsbns);
    }

    [Fact]
    public async Task RunAsync_OneVolumesLookupThrowing_DoesNotCostTheOthers()
    {
        await using var dbContext = CreateDbContext();
        await SeedAsync(dbContext, [(2, null, true), (3, null, true)]);

        var candidateSource = Substitute.For<IIsbnCandidateSource>();
        candidateSource.SourceName.Returns("OpenLibrary");
        candidateSource.FindAsync("Overlord", 2, Arg.Any<CancellationToken>())
            .Returns<Task<IsbnCandidates>>(_ => throw new HttpRequestException("Open Library is down"));
        candidateSource.FindAsync("Overlord", 3, Arg.Any<CancellationToken>())
            .Returns(new IsbnCandidates { WorksFound = 1, Isbns = [EnglishIsbn] });

        var rows = await CreateService(
                dbContext,
                candidateSource,
                Enrichments((EnglishIsbn, EditionLanguage.English, "Yen On")))
            .RunAsync(CancellationToken.None);

        // Per-volume failure isolation, the same rule the sync run follows.
        rows.Count.ShouldBe(2);
        rows.Single(row => row.VolumeNumber is 2).Outcome.ShouldBe(IsbnDiscoveryOutcome.LookupFailed);
        rows.Single(row => row.VolumeNumber is 3).Isbn.ShouldBe(EnglishIsbn);
    }

    [Fact]
    public async Task RunAsync_Always_LeavesTheShelfAlone()
    {
        await using var dbContext = CreateDbContext();
        await SeedAsync(dbContext, [(2, null, true)]);

        await CreateService(
                dbContext,
                Candidates(EnglishIsbn),
                Enrichments((EnglishIsbn, EditionLanguage.English, "Yen On")))
            .RunAsync(CancellationToken.None);

        // The whole point of the two-command split: discovery guesses, and a guess is
        // not shelf data until a person has read it.
        (await dbContext.Volumes.CountAsync(volume => volume.Isbn != null)).ShouldBe(0);
    }

    private static IsbnDiscoveryService CreateService(
        LnShelfDbContext dbContext,
        IIsbnCandidateSource candidateSource,
        IVolumeEnrichmentSource enrichmentSource) =>
        new(dbContext, candidateSource, enrichmentSource, NullLogger<IsbnDiscoveryService>.Instance);

    /// <summary>A search that offers exactly these ISBNs, in this order.</summary>
    private static IIsbnCandidateSource Candidates(params string[] isbns)
    {
        var source = Substitute.For<IIsbnCandidateSource>();
        source.SourceName.Returns("OpenLibrary");
        source.FindAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(isbns.Length is 0
                ? IsbnCandidates.None
                : new IsbnCandidates { WorksFound = 1, Isbns = isbns });

        return source;
    }

    /// <summary>An enrichment source that knows exactly these editions and nothing else.</summary>
    private static IVolumeEnrichmentSource Enrichments(
        params (string Isbn, EditionLanguage Language, string Publisher)[] editions)
    {
        var source = Substitute.For<IVolumeEnrichmentSource>();
        source.SourceName.Returns("OpenLibrary");
        source.GetByIsbnAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((VolumeEnrichment?)null);

        foreach (var (isbn, language, publisher) in editions)
        {
            source.GetByIsbnAsync(isbn, Arg.Any<CancellationToken>()).Returns(new VolumeEnrichment
            {
                SourceRecordKey = $"/books/{isbn}",
                Title = $"Overlord, Volume 2 ({publisher})",
                Publisher = publisher,
                Language = language,
            });
        }

        return source;
    }

    private static async Task SeedAsync(
        LnShelfDbContext dbContext,
        (int Number, string? Isbn, bool Owned)[] volumes)
    {
        var series = new Series
        {
            Id = Guid.CreateVersion7(),
            AniListId = 85976,
            TitleEnglish = "Overlord",
            Format = Format.LightNovel,
            PublicationStatus = PublicationStatus.Ongoing,
        };

        foreach (var (number, isbn, owned) in volumes)
        {
            series.Volumes.Add(new Volume
            {
                Id = Guid.CreateVersion7(),
                Number = number,
                Isbn = isbn,
                UserState = owned ? new VolumeUserState { Owned = true } : null,
            });
        }

        dbContext.Series.Add(series);
        await dbContext.SaveChangesAsync();
    }

    private static LnShelfDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<LnShelfDbContext>()
            .UseInMemoryDatabase($"isbn-discovery-{Guid.CreateVersion7()}")
            .Options);
}
